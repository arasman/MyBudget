# MyBudget — Deployment Lessons Learned

Real-world log of deploying MyBudget to Hetzner for the TFM review period, following `docs/DEPLOYMENT.md`. The guide's happy path didn't hold in several places — this documents what actually happened, why, and how it was fixed. Organized by the guide's own Part numbering.

## Server details

- Name: `my-budget-ubuntu-4gb-nbg1-1`
- Location: Nuremberg (nbg1) — only EU region offered close to the guide's original recommendation
- Image: Ubuntu 26.04 LTS (guide specified 24.04; 26.04 is now the current LTS)
- Type: **CX23** (2 vCPU / 4GB / 40GB) — replaces CX22 from the guide, same spec class, Hetzner renamed the line
- Public IPv4: `178.104.162.216`
- Cost: $6.49/mo server + $0.60/mo IPv4 = **$7.09/mo** (Hetzner now bills IPv4 separately; guide's €3.79 estimate is stale)
- Firewall: TCP 22, 80, 443, ICMP — matches guide
- Domain: `mybudget-aras.duckdns.org` (DuckDNS, free)

## Part 1 — Server provisioning + SSH access

### Hetzner catalog drift (CX22 → CX23, Ubuntu 24.04 → 26.04, IPv4 pricing)

**Symptom:** none of the exact SKUs/images from the guide were selectable in the console.

**Resolution:** not a real problem — just Hetzner's catalog moving on since the guide was written. Picked the direct equivalents (CX23 for CX22, current Ubuntu LTS, closest EU region). No functional impact.

### SSH key rejected — server always fell back to password auth

**Symptom:** `ssh -i key_id_ed25519 root@<ip>` always ended at `root@<ip>'s password:` instead of logging in with the key.

**Diagnosis path:**
1. `ssh -v -i key_id_ed25519 root@<ip>` showed the key *was* being offered (`debug1: Offering public key: ...`), but the server immediately fell through to password auth — meaning the offered key wasn't recognized, not a path/config problem locally.
2. Compared the local key's fingerprint (`ssh-keygen -lf key_id_ed25519.pub`) against the fingerprint Hetzner showed under **Security → SSH Keys** for the key attached to the server. **They didn't match.**

**Root cause:** the wrong/different SSH key ended up attached to the server at creation time (not a copy-paste formatting issue — the `.pub` file itself was a single clean line, verified by inspection).

**Key lesson:** adding or fixing an SSH key resource in the Hetzner console **after** a server exists does nothing for that server — Hetzner only injects the key into `authorized_keys` once, via cloud-init, at creation time. There is no "resync" action. Recreating the key resource was a dead end; the running server had to be fixed directly.

**Fix — via Hetzner Rescue System:**
1. Server detail page (not the list-view "⋮" menu, which only has Power off/Shutdown/Snapshot/Protection/Console/Delete) → **Rescue** tab.
2. **Enable rescue** — Hetzner shows a one-time rescue root password. This only stages rescue mode; it does **not** reboot the server by itself.
3. Reboot to actually enter rescue mode. This server had no "Reboot" button — only **Shutdown** / **Power off** / **Start**. Shutdown → Start achieves the same result.
4. SSH into the rescue environment as root with the rescue password (this triggers a "host key changed" warning — **expected**, the rescue kernel has its own host key, not a real MITM). Cleared with `ssh-keygen -R <ip>` before retrying.
5. `lsblk` to find the real disk partition (`/dev/sda1` on this CX23), then:
   ```bash
   mount /dev/sda1 /mnt
   nano /mnt/root/.ssh/authorized_keys   # replace with correct pubkey content
   umount /mnt
   ```
6. Rescue mode had already auto-reverted to "disabled" after the fix (console only offered "Enable rescue" variants + plain "Power cycle" — no "Disable rescue" button, because it was already off). Power cycled again to boot back into the normal OS.
7. Second boot triggered **another** "host key changed" warning — also expected, this time going rescue-kernel → normal-OS host key. Fingerprint matched the server's *original* host key from the very first connection attempt, confirming it was genuinely back on the normal OS (not another red flag). Cleared with `ssh-keygen -R <ip>` again.

### Root password expiry on first key-based login

**Symptom:** after the key finally worked (`Enter passphrase for key 'key_id_ed25519'` — correctly prompting for the *local* key's passphrase now, not the server), login immediately hit:
```
WARNING: Your password has expired.
You must change your password now and log in again!
```

**Cause:** the password set while patching `authorized_keys` via rescue mode inherited an expired/forced-change policy from the rescue environment's account state. This is orthogonal to SSH key auth — Linux enforces password expiry via PAM independent of which auth method got you a shell.

**Fix:** set a new password when prompted (noted down as a console/Rescue-tab fallback credential — not used for SSH going forward, since Part 1 step 5 disables `PasswordAuthentication` entirely shortly after). Session drops after the forced change; reconnecting with the key logs in cleanly.

### Part 1 steps, as actually executed

Once SSH access was fixed, the rest of the guide's Part 1 ran with no deviation:

```bash
# as root
adduser deploy
usermod -aG sudo deploy
rsync --archive --chown=deploy:deploy ~/.ssh /home/deploy

# verify deploy logs in via key from a separate terminal before touching sshd_config
ssh -i key_id_ed25519 deploy@<ip>

# back in the root session — harden SSH
nano /etc/ssh/sshd_config   # PermitRootLogin no / PasswordAuthentication no
systemctl restart ssh

# verify from a THIRD terminal, keep root session open until this passes
ssh -i key_id_ed25519 deploy@<ip>

# only then: as deploy
curl -fsSL https://get.docker.com | sudo sh   # sudo password = deploy's own password, not root's
sudo usermod -aG docker deploy
exit   # log out/in for group change
docker compose version   # → Docker Compose version v5.4.0
```

Confirmed post-hardening: `ssh -i key_id_ed25519 root@<ip>` now connects but is refused outright (`PermitRootLogin no` in effect) — root access only via `deploy` + `sudo` from here on.

## Part 2 — DNS

DuckDNS domain was initially pointed at the local machine's home IP (leftover from account setup), not the server. Updated the A record to `178.104.162.216` at https://www.duckdns.org/domains, confirmed with `nslookup mybudget-aras.duckdns.org` before continuing — no other issues.

## Part 3 — Brevo SMTP setup

Brevo's UI had moved since the guide was written: SMTP key generation is now gated behind an "Activar para Claves SMTP" (Activate for SMTP keys) toggle before the "Generate a new SMTP key" button appears. Cosmetic drift only, same end result.

## Part 6 — Frontend build: missing toolchain on a fresh box

The guide's Part 6 (`corepack enable`, `pnpm install`, `pnpm build`) assumes Node.js is already present. On a fresh Ubuntu image with only Docker installed (Part 1), none of it existed yet.

**Fixes applied, in order:**
1. **`corepack: command not found`** — Node.js itself wasn't installed. Installed via NodeSource:
   ```bash
   curl -fsSL https://deb.nodesource.com/setup_lts.x | sudo -E bash -
   sudo apt-get install -y nodejs   # → v24.19.0
   ```
2. **`corepack enable` → `EACCES: permission denied`** — needs to write into `/usr/bin`, which the unprivileged `deploy` user can't do:
   ```bash
   sudo corepack enable
   ```
3. **`pnpm install` → `[ERR_PNPM_IGNORED_BUILDS] Ignored build scripts: esbuild@0.28.1`** — pnpm's newer security gate blocks postinstall scripts by default; Vite depends on esbuild's postinstall to fetch its native binary:
   ```bash
   pnpm approve-builds   # select esbuild with space, confirm with Enter
   pnpm install
   pnpm build             # → ✓ built in 3.41s
   ```

**Takeaway:** the guide should either state Node.js is a prerequisite to install in Part 1, or fold these three commands into Part 6 directly. Worth revisiting the guide text itself in a future pass.

## Part 7 — Build and start everything: three real bugs found

`docker compose -f docker-compose.prod.yml up -d --build` did not work cleanly on the first (or second, or third) attempt. Unlike the Part 1 issues above (environment drift), these three were genuine bugs in the repo's deploy configuration, verified and fixed in the codebase itself — not just worked around on the server.

### Bug 1 — API couldn't reach Postgres: missing `libgssapi_krb5.so.2`

**Symptom**, in `docker compose logs api`:
```
Cannot load library libgssapi_krb5.so.2
Error: libgssapi_krb5.so.2: cannot open shared object file: No such file or directory
[ERR]  Failed executing DbCommand ... SELECT "MigrationId", "ProductVersion" FROM "__EFMigrationsHistory" ...
```

**Cause:** the `aspnet:10.0` runtime base image is missing a native library Npgsql (the Postgres driver) tries to load. The guide's own verification ("`docker compose build api` succeeds") only proved the image *builds*, not that it *runs* — exactly the caveat the guide called out in its "What changed in the codebase" section.

**Fix**, in `Project/src/MyBudget.Api/Dockerfile`, runtime stage:
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

RUN useradd --user-group --no-create-home appuser
COPY --from=build /app/publish .
USER appuser
```
(Must run as root, before `USER appuser`.)

### Bug 2 — Seq refused to start: wrong EULA env var name

**Symptom:**
```
The Seq End User License Agreement (...) must be accepted in order to use this Docker image.
Set the ACCEPT_EULA=Y environment variable to indicate acceptance.
```
...repeating in a crash-restart loop, despite `.env` already having `SEQ_ACCEPT_EULA=Y`.

**Cause:** the guide's `.env` template used the wrong variable name. Seq's own image reads this one literally as `ACCEPT_EULA` — no `SEQ_` prefix — unlike most of its other settings (e.g. `SEQ_FIRSTRUN_ADMINPASSWORDHASH`, which genuinely does use the prefix). Since `env_file: .env` just dumps raw key/value pairs into the container with no renaming, a wrong key name fails silently as "not set" rather than erroring on the typo itself.

**Fix**, in `.env` (and the guide's template):
```
ACCEPT_EULA=Y
```

### Bug 3 — Seq refused to start (again): missing admin password / no-auth opt-out

**Symptom**, after fixing Bug 2:
```
System.InvalidOperationException: No default admin password was supplied; set `firstRun.adminPassword`
or `SEQ_FIRSTRUN_ADMINPASSWORD`, or opt out of authentication using
`firstRun.noAuthentication`/`SEQ_FIRSTRUN_NOAUTHENTICATION`.
```

**Cause:** the guide's `.env` template left `SEQ_FIRSTRUN_ADMINPASSWORDHASH=` blank with no fallback. Seq requires either a real password hash or an explicit opt-out on first run — an empty value satisfies neither.

**Fix:** since Seq is never exposed to the internet in this deployment (no `ports:` mapping to the public interface; only reachable via SSH tunnel, per the guide's own security posture in Part 8), the SSH tunnel already *is* the access control. Opted out of Seq's own auth layer rather than generating a password hash:
```
# --- Seq ---
# Seq is never exposed to the internet in this setup (only reachable via SSH
# tunnel, see Part 8) — the tunnel is the access control, so Seq's own auth
# is opted out here rather than generating an admin password hash.
ACCEPT_EULA=Y
SEQ_FIRSTRUN_NOAUTHENTICATION=True
```

### Bug 4 — Seq/Jaeger unreachable via the documented SSH tunnel

Found in Part 8 (smoke test), but it's a Part 7 config bug: `docker-compose.prod.yml` deliberately never publishes Seq or Jaeger to the host (by design — internal container-network only), but Part 8's instructions assume something is already listening on the server's `localhost:5341`/`16686` for the SSH tunnel to forward into. That binding never existed.

**Symptom:**
```
ssh -L 5341:localhost:5341 -L 16686:localhost:16686 ...
channel 5: open failed: connect failed: Connection refused
```

**Fix** — publish both ports, but bound to loopback only (`127.0.0.1:`) so they stay unreachable from the public internet; the SSH tunnel remains the only path in:
```yaml
  seq:
    ports:
      - "127.0.0.1:5341:80"   # NOT 5341:5341 — Seq's web UI lives on
                                # container port 80; container's own 5341 is
                                # a separate legacy ingestion-only endpoint.
  jaeger:
    ports:
      - "127.0.0.1:16686:16686"
```
The Seq port mapping needed a second correction after the first attempt (`5341:5341`) returned `{"Error":"Not found."}` instead of the UI — container port 80 is the actual UI/API port; `5341` is just the conventional *host*-side alias most Seq tutorials use.

## Part 8 — Smoke test: Brevo IP authorization

**Symptom:** registration succeeded, but no welcome email arrived. API logs showed:
```
MailKit.Security.AuthenticationException: 525: 5.7.1 Unauthorized IP address
```
Brevo's Transactional → Logs showed no entry at all — the send never reached Brevo's acceptance stage.

**Cause:** Brevo blocks SMTP relay from IPs it hasn't seen before, as an account security feature — unrelated to the STARTTLS/auth code fix already made earlier in the project.

**Fix:** Brevo dashboard → Security → Authorised IPs → add the server's IP. First attempt failed transiently (`We could not authorize one or more IP addresses. Try again in a few minutes.`); refreshing the page and retrying shortly after succeeded. Confirmed working by triggering a **new** email (the original failed welcome email does not auto-retry) — a password-reset request delivered successfully end-to-end.

## Reusable takeaways for future deploys

- **Always diff the key fingerprint** (`ssh-keygen -lf`) against what the cloud console shows for the attached key *before* assuming a paste/format bug — a silent wrong-key attach looks identical to a formatting issue until you compare hashes.
- **A cloud provider's SSH key resource is not live-synced to a running VM.** Fixing/rotating a key after creation requires direct filesystem access (rescue mode, cloud-init re-run, or an already-working session) — not just re-uploading the key.
- **"Host key changed" warnings are correct behavior**, not something to blindly bypass — but a rescue-mode round trip legitimately changes the host key twice (into rescue, back to normal), so two such warnings in a row can both be expected. Verify by comparing the fingerprint shown against a known-good one (e.g., the very first connection) rather than reflexively trusting or distrusting it.
- **Verify each hardening step from a fresh terminal, keeping the current session open**, before closing it — this caught nothing here, but is what makes a lockout survivable if it does.
- **`docker compose build` succeeding proves the image builds, not that it runs.** Runtime-only failures (missing native libraries, env var name mismatches, first-run requirements) only surface once containers actually start and try to talk to each other.
- **`env_file:` in Compose does no key renaming or validation** — a wrong variable name (`SEQ_ACCEPT_EULA` vs `ACCEPT_EULA`) fails silently as "unset," not as an error pointing at the typo. Cross-check every third-party image's actual expected env var names against its docs, don't assume a prefix pattern holds for all of them.
- **A guide's own stated security intent must be checked against what the config actually does.** "Don't expose Seq/Jaeger to the internet" was true, but the follow-on assumption ("so an SSH tunnel will reach them") silently failed because loopback-only publishing was never added.
- **New sending infrastructure (new server IP, new SMTP relay) often needs its own authorization step with the email provider**, independent of whether the SMTP credentials/auth code themselves are correct.
- Provider catalogs (SKUs, image versions, pricing) and third-party vendor UIs (Brevo's SMTP-key gating) drift over time; treat a deployment guide's specific instance/image names and click-paths as intent, not a literal spec to match exactly.
