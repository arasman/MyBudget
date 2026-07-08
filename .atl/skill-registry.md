# Skill Registry

**Delegator use only.** Any agent that launches sub-agents reads this registry to resolve compact rules, then injects them directly into sub-agent prompts. Sub-agents do NOT read this registry or individual SKILL.md files.

## User Skills

| Trigger | Skill | Path |
|---------|-------|------|
| "vertical slice", "feature folder", "slice", "VSA", "feature pipeline", "self-contained feature", "build with vertical slice" | Vertical Slice Architecture (.NET 10 + Vue 3) | C:/Users/alejandro.alfaro/.claude/skills/skill-net-vertical-slice-architecture/SKILL.md |
| "create a PR", "open a pull request", "submit branch for review" (GitHub) | branch-pr | C:/Users/alejandro.alfaro/.claude/skills/branch-pr/SKILL.md |
| "create a PR", "open a pull request", "submit branch for review" (Azure DevOps) | azure-devops-pr | C:/Users/alejandro.alfaro/.claude/skills/azure-devops-pr/SKILL.md |
| "create a GitHub issue", "report a bug", "request a feature" | issue-creation | C:/Users/alejandro.alfaro/.claude/skills/issue-creation/SKILL.md |
| "judgment day", "judgment-day", "dual review", "doble review", "juzgar" | judgment-day | C:/Users/alejandro.alfaro/.claude/skills/judgment-day/SKILL.md |

## Compact Rules

Pre-digested rules per skill. Delegators copy matching blocks into sub-agent prompts as `## Project Standards (auto-resolved)`.

### skill-net-vertical-slice-architecture

**Structure**
- Organize by feature, NOT by layer: `Features/{Domain}/{Action}/` owns endpoint + handler + validator + persistence
- Each slice = exactly 4 files: `{Action}{Domain}Command.cs`, `{Action}{Domain}Validator.cs`, `{Action}{Domain}Handler.cs`, `{Action}{Domain}Endpoint.cs`
- Slices NEVER reference each other — only SharedKernel
- SharedKernel only for code genuinely needed by 3+ slices

**Backend**
- Use `Mediator` (NuGet: `Mediator.Abstractions`) — NOT MediatR. Handlers return `ValueTask<Result<T>>`
- Command slices use EF Core (`AppDbContext`). Query slices use Dapper via keyed `ConnectionFactory`
- `AppDbContext` is PURE — NEVER inject IMediator into it
- Pipeline behaviours: `ValidationBehaviour`, `LoggingBehaviour`, `CachingBehaviour` — cross-cutting only
- Endpoint: static `Map(IEndpointRouteBuilder)` method, auto-discovered via reflection in `EndpointExtensions.cs`
- Secrets: dev = .NET User Secrets, prod = env vars. NEVER put secrets in `appsettings.json`
- Passwords: BCrypt with `workFactor: 12` — NEVER MD5/SHA1/SHA256 for passwords
- Email: MailKit + Channel pattern — handler writes to Channel (non-blocking), BackgroundService sends SMTP
- i18n backend: `Microsoft.Extensions.Localization` — `.resx` files per handler/validator, inject `IStringLocalizer<T>`

**Frontend (Vue 3)**
- Stack: Vue 3 + Vite + Pinia + TailwindCSS + daisyUI + Axios + Zod + DOMPurify + ESLint + vue-i18n ^9 + chart.js + vue-chartjs
- Package manager: **pnpm** — never npm or yarn
- Axios interceptor: add `X-Correlation-Id` (uuidv4) and `Accept-Language` (from localStorage) on every request
- ESLint: `vue/no-v-html` = error (XSS), `@typescript-eslint/no-explicit-any` = error
- i18n frontend: `vue-i18n ^9`, locales in `src/i18n/`, language stored in localStorage

**Security**
- Never use `v-html` with user content (XSS)
- Never store secrets in appsettings.json
- Validate all inputs with FluentValidation (backend) and Zod (frontend)
- Sanitize user-generated HTML with DOMPurify

### branch-pr

- Create branch from issue number: `feature/{issue-number}-{slug}`
- PR title format: `[#{issue}] Short description`
- PR must reference the issue it resolves
- Squash merge preferred

### azure-devops-pr

- Authenticate via PAT (Personal Access Token) — never username/password
- Use Azure DevOps REST API: `POST https://dev.azure.com/{org}/{project}/_apis/git/repositories/{repo}/pullrequests`
- Target branch: `main` or `develop` — confirm before creating
- Include work item links when available

### issue-creation

- Issue title: imperative verb + clear subject ("Add user authentication", "Fix budget calculation")
- Include: description, expected behavior, affected area
- Label by type: `feat`, `bug`, `chore`, `docs`

### judgment-day

- Launch two INDEPENDENT blind judge sub-agents simultaneously (parallel, not sequential)
- Each judge reviews the same target with NO knowledge of the other's findings
- Synthesize findings: apply fixes, then re-judge (max 2 iterations)
- Escalate to user only if both judges disagree after iteration 2

## Project Conventions

| File | Path | Notes |
|------|------|-------|
| Global CLAUDE.md | C:/Users/alejandro.alfaro/.claude/CLAUDE.md | Global rules: commit style, tool preferences, personality |
| Project proposal | D:/Projects/bigschool/TFM/MyBudget/AnalisisInicial/PlanteamientoDeProyecto.txt | MVP A scope, stack, roles, i18n, deployment notes |
| Current situation | D:/Projects/bigschool/TFM/MyBudget/AnalisisInicial/SituacionActual.txt | Domain model from Excel — source of truth for budget domain |
| Tutor conversation | D:/Projects/bigschool/TFM/MyBudget/AnalisisInicial/Tutor-Conversación.txt | TFM evaluation criteria and requirements |
