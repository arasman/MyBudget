# git-setup Specification

## Purpose

Define the git repository initialisation, `.gitignore` rules, branch strategy, and versioning policy for MyBudget. This is a prerequisite for all other capabilities.

## Requirements

### Requirement: Repository Initialisation

A git repository MUST be initialised at `D:/Projects/bigschool/TFM/MyBudget/`. The default branch MUST be named `main`. At least one commit MUST exist on `main` before any feature branch is created. All work for the foundation change MUST be committed on a branch named `feature/foundation`.

#### Scenario: Repository exists with main branch

- GIVEN the scaffold has been applied
- WHEN `git log --oneline` is executed at the repo root
- THEN at least one commit is shown on the `main` branch

#### Scenario: Foundation work is on feature branch

- GIVEN the scaffold has been applied
- WHEN `git branch` is executed
- THEN `feature/foundation` exists and contains the scaffold commits

---

### Requirement: .gitignore Coverage

A `.gitignore` file MUST exist at the repo root and MUST exclude the following: `AnalisisInicial/`, `bin/`, `obj/`, `.vs/`, `*.user`, `node_modules/`, `dist/`, `.env.local`, `.env`, `coverage/`, `*.user.json`. The `openspec/` and `.atl/` directories MUST NOT be excluded — they SHALL be versioned.

#### Scenario: Generated artifacts are not tracked

- GIVEN the project has been built (bin/ and obj/ exist) and frontend installed (node_modules/ exists)
- WHEN `git status` is checked
- THEN `bin/`, `obj/`, and `node_modules/` do not appear in staged or untracked files

#### Scenario: SDD artifacts are tracked

- GIVEN `openspec/` and `.atl/` directories contain files
- WHEN `git status` is checked
- THEN files in `openspec/` and `.atl/` appear as tracked (not ignored)

#### Scenario: Secrets file is not tracked

- GIVEN `.env` exists at the repo root with credential values
- WHEN `git status` is checked
- THEN `.env` does not appear in staged or untracked files

---

### Requirement: Commit Convention

All commits MUST follow Conventional Commits format (`type(scope): message`). No AI attribution or `Co-Authored-By` trailers MAY appear in commit messages.

#### Scenario: Commit message follows conventional format

- GIVEN a commit is created for the backend scaffold
- WHEN `git log --format="%s" -1` is executed
- THEN the subject matches the pattern `^(feat|chore|fix|docs|refactor|test|ci)\(.*\): .+`
