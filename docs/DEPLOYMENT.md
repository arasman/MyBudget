# MyBudget — Deployment Guide

Reference document for deploying MyBudget to a public URL for the TFM review period. Read this in full before touching any real infrastructure — it explains _why_ each step exists, not just the command to run.

**Read this today. Execute tomorrow, one section at a time, in order.**

## Table of Contents

- [What we're deploying, and why this shape](#what-were-deploying-and-why-this-shape)
- [Cost](#cost)
- [Prerequisites](#prerequisites)
- [Part 1 — Provision the server](#part-1--provision-the-server)
- [Part 2 — Point DNS at the server](#part-2--point-dns-at-the-server)
- [Part 3 — Brevo (SMTP for real email)](#part-3--brevo-smtp-for-real-email)
- [Part 4 — Get the code onto the server](#part-4--get-the-code-onto-the-server)
- [Part 5 — Configure secrets (`.env`)](#part-5--configure-secrets-env)
- [Part 6 — Build the frontend](#part-6--build-the-frontend)
- [Part 7 — Build and start everything](#part-7--build-and-start-everything)
- [Part 8 — Smoke test](#part-8--smoke-test)
- [Part 9 — Ongoing operations](#part-9--ongoing-operations)
- [Part 10 — Decommission / cost control](#part-10--decommission--cost-control)
- [What changed in the codebase to make this possible](#what-changed-in-the-codebase-to-make-this-possible)
- [Troubleshooting](#troubleshooting)

## What we're deploying, and why this shape

One Hetzner VPS running the **exact same 5 services** as local dev (`docker-compose.yml`: Postgres, Redis, Seq, Jaeger, API), plus a reverse proxy (Caddy) in front, serving the built Vue frontend and terminating HTTPS. Everything lives behind one domain — no separate frontend host, no CORS to configure (the app currently has none, and doesn't need one this way).

Why this over a managed PaaS (Railway/Render): those bill per-service, per-resource — running Postgres + Redis + Seq + Jaeger + API as 5 separately-billed services would land well past this project's budget. A flat-rate VPS running the same containers together is the only option that keeps the full observability stack **and** stays cheap. See the `project/tfm-deploy-strategy` decision for the fuller cost comparison.

Trade-off, stated plainly: nothing here is managed. No automatic backups, no failover, no monitoring alerts. You are the ops team for the review window. That's an acceptable trade for a 2-3 month TFM review, not for a long-lived production app.

## Cost

| Item                                          | Cost                                         |
| --------------------------------------------- | -------------------------------------------- |
| Hetzner CX22 (2 vCPU / 4GB RAM / 40GB SSD)    | ~€3.79/mo (~$4.1)                            |
| Domain (if you don't already own one)         | varies, or use a free DuckDNS subdomain — $0 |
| Brevo SMTP                                    | $0 (free tier, 300 emails/day)               |
| Let's Encrypt TLS cert (via Caddy, automatic) | $0                                           |

Roughly $4-5/mo all-in. For a 2-3 month review window, that's the number to compare against your budget.

## Prerequisites

- [ ] Hetzner Cloud account (hetzner.com) — needs a payment method on file even to use low-cost servers
- [ ] A domain name you control, **or** a free subdomain from [duckdns.org](https://www.duckdns.org) — Caddy's automatic HTTPS requires a real DNS name pointing at the server; a bare IP address cannot get a Let's Encrypt certificate
- [ ] An SSH key pair. If you don't have one: `ssh-keygen -t ed25519 -C "your_email@example.com"` (run locally, on your Windows machine — e.g. in Git Bash)
- [ ] A free [Brevo](https://www.brevo.com) account
- [ ] Repo already pushed to GitHub (done — `https://github.com/arasman/MyBudget`)

## Part 1 — Provision the server

1. Hetzner Cloud console → **New Project** → **Add Server**.
   - Location: closest to you.
   - Image: **Ubuntu 24.04 LTS**.
   - Type: **CX22** (2 vCPU / 4GB / 40GB — the one from the cost comparison).
   - SSH key: paste your **public** key (`~/.ssh/id_ed25519.pub`) here at creation time — do this now, not after, so you never need password SSH login.
   - Firewall: create one now (or right after) allowing inbound TCP 22, 80, 443 only. Deny everything else.
2. Note the server's public IPv4 address.
3. SSH in as root the first time: `ssh root@<server-ip>`.
4. Create a non-root sudo user (don't operate as root day-to-day):
   ```bash
   adduser deploy
   usermod -aG sudo deploy
   rsync --archive --chown=deploy:deploy ~/.ssh /home/deploy
   ```
5. Harden SSH — edit `/etc/ssh/sshd_config`: set `PermitRootLogin no` and `PasswordAuthentication no`, then `systemctl restart ssh`.
6. From here on, SSH in as `ssh deploy@<server-ip>`.
7. Install Docker Engine + Compose plugin (official convenience script, Ubuntu-supported):
   ```bash
   curl -fsSL https://get.docker.com | sudo sh
   sudo usermod -aG docker deploy
   ```
   Log out and back in for the group change to apply, then confirm: `docker compose version`.

## Part 2 — Point DNS at the server

- Domain you own: add an **A record** for the subdomain you'll use (e.g. `mybudget.yourdomain.com`) pointing at the server's IPv4.
- No domain: sign up at [duckdns.org](https://www.duckdns.org), create a subdomain (e.g. `mybudget-tfm.duckdns.org`), point it at the server's IP from the DuckDNS dashboard.

Wait for propagation before Part 7 — check with `nslookup mybudget.yourdomain.com` from your own machine. Caddy will fail to get a certificate if DNS isn't resolving yet.

## Part 3 — Brevo (SMTP for real email)

The app sends real emails (registration welcome, invites, password reset) via SMTP. Mailpit — the dev-only fake mailbox — doesn't exist in this deployment; Brevo replaces it.

1. Sign up free at brevo.com.
2. Verify your sender email address (Brevo requires this before it'll relay mail from that address — check your inbox for their verification link).
3. Go to **SMTP & API** settings → **SMTP** tab. Note:
   - SMTP server: `smtp-relay.brevo.com`
   - Port: `587`
   - Login: your Brevo account email
   - Password: **not** your account password — click "Generate a new SMTP key" and use that value instead.

Keep these four values handy for Part 5.

## Part 4 — Get the code onto the server

```bash
git clone https://github.com/arasman/MyBudget.git
cd MyBudget/Project
```

## Part 5 — Configure secrets (`.env`)

Create `.env` in `Project/` on the server (**never commit this file** — it's already gitignored):

```bash
# --- Postgres (container-internal only, never exposed to the internet) ---
POSTGRES_USER=mybudget
POSTGRES_PASSWORD=<generate: openssl rand -base64 24>
POSTGRES_DB=mybudget

# --- Seq ---
# Seq is never exposed to the internet in this setup (only reachable via SSH
# tunnel, see Part 8) — the tunnel is the access control, so Seq's own auth
# is opted out here rather than generating an admin password hash.
ACCEPT_EULA=Y
SEQ_FIRSTRUN_NOAUTHENTICATION=True

# --- JWT signing key (must be 32+ chars) ---
JWT__Key=<generate: openssl rand -base64 48>

# --- Brevo SMTP (from Part 3) ---
Email__SmtpHost=smtp-relay.brevo.com
Email__SmtpPort=587
Email__SmtpUseStartTls=true
Email__SmtpUsername=<your Brevo account email>
Email__SmtpPassword=<your Brevo SMTP key>
Email__FromAddress=<your verified Brevo sender address>
Email__FromName=MyBudget

# --- Frontend URL (used to build links inside emails — invites, password reset) ---
App__FrontendBaseUrl=https://mybudget.yourdomain.com

# --- Caddy (reverse proxy domain — see Part 7) ---
SITE_DOMAIN=mybudget.yourdomain.com
```

Notes:

- `POSTGRES_*` are the same variable names `Project/.env.example` already uses for local dev — just with a real generated password instead of `change_me`.
- `SITE_DOMAIN` is read directly by `Caddyfile` (`{$SITE_DOMAIN} { ... }`, Caddy's native env-var substitution) via the `caddy` service's `env_file: .env` in `docker-compose.prod.yml`. This is the **only** place you set the domain — `Caddyfile` itself stays generic and never needs manual editing, so `git pull` never conflicts with it.
- The double-underscore keys (`JWT__Key`, `Email__SmtpHost`, `App__FrontendBaseUrl`, ...) are ASP.NET Core's convention for environment variables mapping to nested `appsettings.json` sections (`JWT:Key`, `Email:SmtpHost`, `App:FrontendBaseUrl`). Get the underscore count right — `__` (double), not `_`.
- You do **not** need to set `ConnectionStrings__DefaultConnection`, `ASPNETCORE_ENVIRONMENT`, or `OTEL_EXPORTER_OTLP_ENDPOINT` here — `docker-compose.prod.yml` builds those automatically from the `POSTGRES_*` values above and points them at the container network. One less place to make a typo.

## Part 6 — Build the frontend

Caddy serves the frontend as static files from `frontend/dist/` (bind-mounted, not containerized) — build it once on the server:

```bash
cd frontend
corepack enable
pnpm install
pnpm build
cd ..
```

This produces `frontend/dist/`. Re-run this (and restart Caddy — Part 9) any time frontend code changes.

## Part 7 — Build and start everything

Domain comes from `SITE_DOMAIN` in `.env` (Part 5) — `Caddyfile` reads it automatically, nothing to edit here.

```bash
docker compose -f docker-compose.prod.yml up -d --build
```

First run pulls base images and builds the API — expect a few minutes. Watch it come up:

```bash
docker compose -f docker-compose.prod.yml logs -f api
```

Look for the EF Core migration log lines (the API auto-migrates the database on startup — normal, no manual step needed), then the "Now listening on" line.

## Part 8 — Smoke test

1. Visit `https://mybudget.yourdomain.com` — should load the app over HTTPS with a valid Caddy/Let's Encrypt certificate (padlock, no warnings).
2. Register a new account. Check the email actually arrives (real inbox this time, not Mailpit) — confirms Brevo auth/STARTTLS is wired correctly.
3. Log in, create a budget, add a category — confirms Postgres + migrations are healthy.
4. Confirm observability, via SSH tunnel (don't expose Seq/Jaeger ports publicly — see below):
   ```bash
   # from your local machine
   ssh -L 5341:localhost:5341 -L 16686:localhost:16686 deploy@<server-ip>
   ```
   Then open `http://localhost:5341` (Seq — should show structured logs from the API) and `http://localhost:16686` (Jaeger — should show traces) in your local browser, tunneled through SSH.

If all four pass, the deployment matches the local architecture end-to-end.

## Part 9 — Ongoing operations

**Redeploy after a code change:**

The server may carry local-only overrides that never got committed to `main` (config tweaks discovered only in production — e.g. a pnpm build-script setting). `git pull` alone can conflict with those. Use the stash-aware sequence below every time, not the bare `git pull`.

```bash
# 1. Connect and go to the repo.
ssh -i <your-key> deploy@<server-ip>
cd ~/MyBudget

# 2. Check for drift before touching anything. Expect either a clean tree,
#    or only known server-only overrides (check git log / prior notes for
#    which files those are — currently: Project/frontend/pnpm-workspace.yaml).
git status

# 3. Stash local overrides, pull, restore them.
git stash
git pull
git stash pop

# 4. If step 3's pop reports a conflict:
#    - Inspect: git status / git diff <file>
#    - If main's incoming version already supersedes the local override
#      (the same fix landed upstream since): keep HEAD's version —
#        git checkout --ours <file>
#        git add <file>
#    - If it's a genuine divergence: merge by hand, then git add <file>
git status
git stash drop   # only after confirming the resolution is correct

# 5. Rebuild frontend (skip if only backend changed).
cd Project/frontend
pnpm install
pnpm build
cd ..

# 6. Rebuild/refresh containers — named volumes (postgres-data, seq-data,
#    caddy-data, caddy-config) persist; only `api` has a build context so
#    only that image actually rebuilds.
docker compose -f docker-compose.prod.yml up -d --build

# 7. Verify Caddy came up clean — look for TLS cert issuance for the right
#    domain, no parse/ACME errors.
docker compose -f docker-compose.prod.yml logs caddy
```

Then smoke test: load the live URL and confirm whatever changed this cycle is actually there.

If a local override turns out to be a real, still-needed fix (not something already merged upstream), consider committing it properly to `main` instead of carrying it forever — see the `SITE_DOMAIN`/`Caddyfile` entry in the codebase-changes table below for an example of retiring one this way.

**View logs:** `docker compose -f docker-compose.prod.yml logs -f <service>` (`api`, `postgres`, `caddy`, ...).

**Backup Postgres** (run periodically, download the file off-server):

```bash
docker compose -f docker-compose.prod.yml exec postgres \
  pg_dump -U mybudget mybudget > backup-$(date +%F).sql
```

**Check what's actually running:** `docker compose -f docker-compose.prod.yml ps`.

## Part 10 — Decommission / cost control

When the TFM review window ends and you're not continuing this into production:

```bash
docker compose -f docker-compose.prod.yml down
```

Then delete the Hetzner server from the console (stops billing immediately — Hetzner bills hourly, so there's no "wasted" partial month). Take a final Postgres backup first if you want the data.

## What changed in the codebase to make this possible

Found while preparing this guide — worth knowing what changed and why, since these weren't deploy-config-only changes:

| File                                                                         | Change                                                                                                                                  | Why                                                                                                                                                                                                                                                                       |
| ---------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Project/src/MyBudget.Api/Dockerfile`                                        | **New.** Multi-stage build (SDK → aspnet runtime), non-root user, port 8080.                                                            | Referenced by `docker-compose.yml` but never existed — `docker compose --profile full` would have failed to build.                                                                                                                                                        |
| `Project/docker-compose.yml`                                                 | `api.build.context` changed from `./src/MyBudget.Api` to `.` (with `dockerfile: src/MyBudget.Api/Dockerfile`).                          | The API project references the sibling `MyBudget.Features` project — Docker build contexts can't reach outside themselves, so the old context couldn't have worked.                                                                                                       |
| `Project/.dockerignore`                                                      | **New.** Excludes `bin/`, `obj/`, `node_modules/`, etc.                                                                                 | Without it, local Windows build artifacts (with Windows-only NuGet fallback paths baked in) get copied into the Linux image and break the container's own restore. Hit this exact failure verifying the Dockerfile today.                                                 |
| `Project/src/MyBudget.Features/SharedKernel/Email/EmailBackgroundService.cs` | Added SMTP authentication (`AuthenticateAsync`) and STARTTLS support (`Email:SmtpUseStartTls` config), both opt-in via new config keys. | Previously hardcoded `SecureSocketOptions.None` and never authenticated — works against Mailpit (no auth needed) but would silently fail against any real SMTP relay like Brevo. Defaults are unchanged, so local dev with Mailpit is unaffected.                         |
| `Project/src/MyBudget.Api/appsettings.Production.json`                       | **New.** Overrides the Seq sink's `serverUrl` to `http://seq:80`.                                                                       | `appsettings.json` hardcodes `http://localhost:5341` for Seq, which only resolves in local dev. Inside the container network, Seq is reachable at `seq:80` (its service name, internal port). Without this, structured logs would silently never reach Seq in production. |
| `Project/Caddyfile`, `Project/docker-compose.prod.yml`                       | `Caddyfile` domain line changed to `{$SITE_DOMAIN}` (Caddy's native env-var substitution); `caddy` service in `docker-compose.prod.yml` gained `env_file: .env`. | The domain used to be hardcoded directly in `Caddyfile`, which meant every server had an uncommitted local edit that conflicted with `git pull` on every redeploy. Moving it into `.env` (Part 5) keeps `Caddyfile` generic and commit-clean. |

All five were verified today: `dotnet build` clean, `docker compose build api` succeeds, `Caddyfile` passes `caddy validate`. The full end-to-end run (Part 8) is still tomorrow's job — building the image is not the same as proving the whole stack works together.

## Troubleshooting

- **Caddy won't get a certificate** — DNS hasn't propagated yet, or port 80/443 is blocked by the Hetzner firewall. Check `docker compose -f docker-compose.prod.yml logs caddy`.
- **API container keeps restarting** — almost always a missing/wrong `.env` value. `docker compose -f docker-compose.prod.yml logs api` will show the startup guard's error (e.g. "JWT\_\_Key is not configured") directly.
- **Emails don't arrive** — check Brevo's dashboard under **Transactional** → **Logs** first; it'll show whether Brevo rejected the send (bad auth, unverified sender) before you go looking at the API. Also double check `Email__SmtpUseStartTls=true` is actually set — without it, the connection attempt to Brevo will fail outright.
- **Seq/Jaeger show nothing** — confirm the SSH tunnel is actually up, and that you're hitting `localhost:5341`/`16686` (the tunnel's local end), not the server's public IP (those ports aren't published there by design).
- **Deployment lesson learned** - During the deployment to Hetzner, some issues arose and have been documented in [`Deployment-LessonLearned.md`](Deployment-LessonLearned.md) where it can find details of lesson learned during [mybudget-aras.duckdns.org](https://mybudget-aras.duckdns.org) deployment process.
