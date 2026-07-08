# MyBudget

Personal finance management application — .NET 10 API + Vue 3 frontend.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 20+](https://nodejs.org/)
- [pnpm](https://pnpm.io/installation) (frontend package manager — no npm or yarn)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- dotnet-ef tool: 

## Quick Start

### 1. Start infrastructure



This starts PostgreSQL, Redis, Mailpit, Seq, and Jaeger.

### 2. Configure secrets

Copy the environment template and configure your local values:



Set the local development connection string via User Secrets:



### 3. Run the API



The API migrates the database automatically on startup.

### 4. Run the frontend


[ERR_PNPM_NO_PKG_MANIFEST] No package.json found in D:\Projects\bigschool\TFM\MyBudget
[ERR_PNPM_NO_PKG_MANIFEST] No package.json found in D:\Projects\bigschool\TFM\MyBudget
[ERROR] Command failed with exit code 1: "C:\nvm4w\nodejs\node.exe" "C:\Users\alejandro.alfaro\AppData\Roaming\npm\node_modules\pnpm\bin\pnpm.mjs" install

pnpm: Command failed with exit code 1: "C:\nvm4w\nodejs\node.exe" "C:\Users\alejandro.alfaro\AppData\Roaming\npm\node_modules\pnpm\bin\pnpm.mjs" install
    at getFinalError (file:///C:/Users/alejandro.alfaro/AppData/Roaming/npm/node_modules/pnpm/dist/pnpm.mjs:29514:14)
    at makeError (file:///C:/Users/alejandro.alfaro/AppData/Roaming/npm/node_modules/pnpm/dist/pnpm.mjs:31821:21)
    at getSyncResult (file:///C:/Users/alejandro.alfaro/AppData/Roaming/npm/node_modules/pnpm/dist/pnpm.mjs:33665:10)
    at spawnSubprocessSync (file:///C:/Users/alejandro.alfaro/AppData/Roaming/npm/node_modules/pnpm/dist/pnpm.mjs:33625:14)
    at execaCoreSync (file:///C:/Users/alejandro.alfaro/AppData/Roaming/npm/node_modules/pnpm/dist/pnpm.mjs:33555:23)
    at callBoundExeca (file:///C:/Users/alejandro.alfaro/AppData/Roaming/npm/node_modules/pnpm/dist/pnpm.mjs:36083:23)
    at boundExeca (file:///C:/Users/alejandro.alfaro/AppData/Roaming/npm/node_modules/pnpm/dist/pnpm.mjs:36060:49)
    at sync (file:///C:/Users/alejandro.alfaro/AppData/Roaming/npm/node_modules/pnpm/dist/pnpm.mjs:36219:10)
    at runPnpmCli (file:///C:/Users/alejandro.alfaro/AppData/Roaming/npm/node_modules/pnpm/dist/pnpm.mjs:213566:5)
    at runDepsStatusCheck (file:///C:/Users/alejandro.alfaro/AppData/Roaming/npm/node_modules/pnpm/dist/pnpm.mjs:215280:7)

Open http://localhost:5173

## Project Structure



## Architecture

### Vertical Slice Architecture (VSA)

Each feature is a self-contained slice with exactly 4 files:
1.  /  — request + handler
2.  — FluentValidation rules
3.  — minimal API endpoint with static  method
4.  /  — request/response DTOs

**Rules:**
- Slices NEVER reference each other — only SharedKernel
- SharedKernel types are only created when used by 3+ slices
- Handlers return 

### Pipeline Behaviours (in order)



### Tailwind v4 CSS-Only (Intentional)

There is NO  — this is by design (ADR-004). Tailwind v4 is configured
entirely in CSS via  using , , and  directives.
daisyUI v5 is configured with . Do not add a .

## EF Core Migration Workflow

### Add a migration

Run from the  directory:

MSBUILD : error MSB1009: Project file does not exist.
Switch: D:\Projects\bigschool\TFM\MyBudget\src\MyBudget.Features
Unable to retrieve project metadata. Ensure it's an SDK-style project. If you're using a custom BaseIntermediateOutputPath or MSBuildProjectExtensionsPath values, Use the --msbuildprojectextensionspath option.

### Apply manually (optional)

The app auto-migrates on startup ( in ). Manual apply:

MSBUILD : error MSB1009: Project file does not exist.
Switch: D:\Projects\bigschool\TFM\MyBudget\src\MyBudget.Features
Unable to retrieve project metadata. Ensure it's an SDK-style project. If you're using a custom BaseIntermediateOutputPath or MSBuildProjectExtensionsPath values, Use the --msbuildprojectextensionspath option.

### Multi-branch migration conflicts

If two branches add migrations concurrently, on merge the second developer must:
1. Delete their migration: No project was found. Change the current working directory or use the --project option.
2. Pull latest 
3. Re-run No project was found. Change the current working directory or use the --project option. to get a fresh timestamp after the merged migration

## Running Tests

MSBUILD : error MSB1009: Project file does not exist.
Switch: MyBudget.slnx

## Known Limitations

1. **MigrateAsync race condition**:  in  causes
   a race condition on multi-instance horizontal deployments. Acceptable for TFM scope (single instance).

2. **NullCacheService**: Redis caching is deferred —  is registered as a no-op
   () until the dedicated  feature change is implemented. The pipeline
   behaviour () is wired and ready; adding Redis requires only implementing
    and swapping the DI registration in .
