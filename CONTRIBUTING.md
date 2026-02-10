# Contributing to App Mod Booster

This repository uses an **agent-driven development model** where AI agents generate the application code and infrastructure from prompts and guardrails. This guide explains how to contribute effectively.

## Repository Structure

```
Blueprint (Source of Truth)          Agent-Generated (Rebuilt)
─────────────────────────────        ─────────────────────────
.github/copilot-instructions.md  →   src/ExpenseManagement/
.github/agents/                  →   deploy-infra/
prompts/                         →   deploy-app/
COMMON-ERRORS.md                 →   deploy-all.ps1
Database-Schema/                 →   .github/workflows/
stored-procedures.sql            →   tests/
Legacy-Screenshots/              →   .deployment-context.json
```

**Key principle:** Never edit agent-generated code directly. Always trace issues back to prompts or guardrails.

## Branch Strategy

This repository uses a two-branch model to separate the **source of truth** (prompts and guardrails) from the **agent-generated application**.

### Branch Overview

| Branch | Purpose | Who Commits | Protected |
|--------|---------|-------------|-----------|
| `blueprint` | Source of truth — prompts, guardrails, schema | Humans only | Yes |
| `release` | Full working application built by agents | Agents + humans (merge only) | Yes |

### How the Branches Relate

```
  blueprint                           release
  ─────────                           ───────
  │                                   │
  │  Prompts, guardrails,             │  Full app: src/, deploy-infra/,
  │  COMMON-ERRORS.md,                │  deploy-app/, .github/workflows/,
  │  Database-Schema/, etc.           │  tests/, deploy-all.ps1, etc.
  │                                   │
  ├── Human updates prompt  ──────┐   │
  │                               │   │
  │                          merge│into│release
  │                               │   │
  │                               └──→├── Agent build runs
  │                                   │
  │                                   ├── CI/CD deploys to Azure
  │                                   │
```

### Key Rules

1. **`blueprint` never contains agent-generated code** — if you see `src/`, `deploy-infra/`, or `tests/` on this branch, something went wrong
2. **`release` always starts from `blueprint`** — before each agent build, merge the latest blueprint in
3. **Bug fixes go to `blueprint` first** — then propagate to `release` via merge + rebuild
4. **Never commit code fixes directly to `release`** — they'll be lost on the next rebuild

### One-Time Setup: Creating the Branches

If the branches don't exist yet, create them from a clean repo state (prompts and guardrails only, no agent code):

```powershell
# Ensure you're on main with only blueprint content (no agent-generated code)
git checkout main

# Create the blueprint branch from current clean state
git checkout -b blueprint
git push -u origin blueprint

# Create the release branch (starts identical, agents will add to it)
git checkout -b release
git push -u origin release

# Set blueprint as the default branch in GitHub:
#   → GitHub.com → Settings → General → Default branch → Change to "blueprint"
```

### Protecting the Branches (GitHub Settings)

Navigate to **GitHub.com → Repository → Settings → Branches → Add branch protection rule**:

**For `blueprint`:**
- Branch name pattern: `blueprint`
- ✅ Require a pull request before merging
- ✅ Require approvals (1 minimum)
- ✅ Do not allow bypassing the above settings (optional, enforces reviews)
- ❌ Do not enable "Require status checks" (no CI runs on blueprint — there's no code to test)

**For `release`:**
- Branch name pattern: `release`
- ✅ Require a pull request before merging
- ✅ Require status checks to pass before merging (once CI/CD exists)
- ✅ Require branches to be up to date before merging

## Bug Fix Procedure

When a bug is discovered in the running application:

### Step 1: Diagnose the Root Cause

Determine whether the bug is in:
- **Agent-generated code** → Fix requires updating prompts/guardrails in `blueprint`
- **Blueprint content** → Fix directly in `blueprint` (rare — e.g., wrong SQL schema)

### Step 2: Update the Blueprint

Edit the appropriate files:

| File | When to Update |
|------|----------------|
| `prompts/prompt-XXX` | Agent generated wrong code pattern |
| `.github/copilot-instructions.md` | Missing rule that would prevent the bug |
| `COMMON-ERRORS.md` | Document the pattern for future reference |
| `.github/agents/*.md` | Specialist agent needs domain-specific guidance |

### Step 3: Rebuild and Verify

```powershell
# 1. Switch to release and clean agent-generated content
git checkout release
Remove-Item -Recurse -Force src, deploy-infra, deploy-app, tests -ErrorAction SilentlyContinue
Remove-Item -Force deploy-all.ps1, .deployment-context.json -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force .github/workflows -ErrorAction SilentlyContinue

# 2. Merge latest blueprint into release
git merge blueprint

# 3. Run agent build (follow prompts/prompt-order)

# 4. Commit and push
git add -A
git commit -m "rebuild: apply blueprint updates"
git push
```

---

## Concrete Example: SqlClient TLS Bug

This example walks through a real bug from discovery to fix, showing every git command.

### The Bug

**Symptom:** Application deployed successfully but failed to start within 10 minutes on Azure App Service.

**Error in logs:**
```
Microsoft.Data.SqlClient.SqlException: Connection reset by peer
System.Net.Sockets.SocketException (104): Connection reset by peer
```

### Step 1: Diagnose

Investigation revealed:
- `Microsoft.Data.SqlClient` version 5.1.5 was being used
- Linux App Service uses OpenSSL 3.0
- SqlClient 5.1.x has a TLS handshake bug with OpenSSL 3.0
- Version 5.2.2+ fixes this issue

**Root cause:** The agent chose version 5.1.5 because no version was specified in the prompts.

### Step 2: Create a Feature Branch from Blueprint and Update

```powershell
# Start from the blueprint branch
git checkout blueprint
git pull origin blueprint

# Create a feature branch for this fix
git checkout -b fix/sqlclient-tls-version
```

Edit three files to add the guardrail:

#### prompts/prompt-004-create-app-code
```markdown
## NuGet Package Versions

Use these specific versions to avoid runtime issues on Linux App Service:

\`\`\`xml
<PackageReference Include="Microsoft.Data.SqlClient" Version="5.2.2" />
\`\`\`

**Important:** Version 5.1.x fails on Linux with "Connection reset by peer" 
due to OpenSSL 3.0 TLS incompatibility.
```

#### .github/copilot-instructions.md
Added to Common Pitfalls:
```markdown
14. **Microsoft.Data.SqlClient version** → must be 5.2.2+; version 5.1.x 
    fails on Linux App Service with "Connection reset by peer" due to 
    OpenSSL 3.0 TLS incompatibility
```

#### COMMON-ERRORS.md
Added full error documentation with bad/good code examples.

### Step 3: Push and Create a Pull Request into Blueprint

```powershell
# Commit the guardrail updates
git add prompts/prompt-004-create-app-code .github/copilot-instructions.md COMMON-ERRORS.md
git commit -m "fix: add SqlClient 5.2.2 minimum version to prompts and guardrails"
git push -u origin fix/sqlclient-tls-version
```

Then on **GitHub.com**:
1. Navigate to the repository
2. Click **"Compare & pull request"** (or go to Pull Requests → New)
3. Set **base branch** to `blueprint` and **compare** to `fix/sqlclient-tls-version`
4. Title: `fix: SqlClient TLS version guardrail`
5. Description: explain the bug, root cause, and what was updated
6. Request review if branch protection requires it
7. **Merge** the PR once approved

### Step 4: Rebuild Release from Updated Blueprint

```powershell
# Switch to release branch
git checkout release
git pull origin release

# Clean all agent-generated content
Remove-Item -Recurse -Force src, deploy-infra, deploy-app, tests -ErrorAction SilentlyContinue
Remove-Item -Force deploy-all.ps1, .deployment-context.json -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force .github/workflows -ErrorAction SilentlyContinue

# Merge the updated blueprint into release
git merge blueprint -m "merge: updated blueprint with SqlClient TLS fix"
```

At this point `release` has the updated prompts but no agent-generated code.

### Step 5: Run Agent Build

Run the agents following `prompts/prompt-order`. The agents will now:
- Read `prompt-004-create-app-code` which specifies `SqlClient 5.2.2`
- Read `copilot-instructions.md` which warns against 5.1.x
- Generate the correct `.csproj` with the right version

### Step 6: Commit and Push the Rebuilt Application

```powershell
# Stage everything the agents generated
git add -A
git commit -m "rebuild: full agent rebuild with SqlClient TLS fix"
git push origin release
```

The CI/CD pipeline (on `release`) triggers and deploys to Azure.

### Step 7: Verify

Check the deployed application starts successfully. The App Service logs should show no TLS errors.

### Summary of Git Commands (Complete Flow)

```powershell
# ── Fix the blueprint ──
git checkout blueprint
git pull origin blueprint
git checkout -b fix/sqlclient-tls-version
# ... edit prompts and guardrails ...
git add -A
git commit -m "fix: add SqlClient 5.2.2 minimum version"
git push -u origin fix/sqlclient-tls-version
# → Create PR on GitHub: fix/sqlclient-tls-version → blueprint
# → Merge PR

# ── Rebuild release ──
git checkout release
git pull origin release
Remove-Item -Recurse -Force src, deploy-infra, deploy-app, tests -ErrorAction SilentlyContinue
Remove-Item -Force deploy-all.ps1, .deployment-context.json -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force .github/workflows -ErrorAction SilentlyContinue
git merge blueprint -m "merge: updated blueprint"
# → Run agent build following prompts/prompt-order
git add -A
git commit -m "rebuild: full agent rebuild"
git push origin release
# → CI/CD deploys to Azure
```

### Why This Works

| Before | After |
|--------|-------|
| Agent picked 5.1.5 (common in training data) | Agent reads prompt specifying 5.2.2 |
| No guardrail warned about version | Pitfall #14 prevents wrong version |
| Bug could recur on rebuild | Fix is permanent across all builds |

---

## Files Reference

### Guardrail Files (Update for Bug Prevention)

| File | Purpose | Update When |
|------|---------|-------------|
| `.github/copilot-instructions.md` | Rules ALL agents follow | Any cross-cutting bug pattern |
| `COMMON-ERRORS.md` | Detailed error patterns with examples | Any bug worth documenting |
| `.github/agents/*.md` | Domain-specific agent rules | Specialist agent makes repeated mistakes |

### Prompt Files (Update for Feature/Behavior Changes)

| File | Controls |
|------|----------|
| `prompt-001-create-app-service` | App Service Bicep module |
| `prompt-002-create-azure-sql` | Azure SQL Bicep module |
| `prompt-004-create-app-code` | ASP.NET application code |
| `prompt-005-deploy-app-code` | Application deployment script |
| `prompt-007-add-api-code` | API controllers |
| `prompt-009-create-genai-resources` | Azure OpenAI infrastructure |
| `prompt-010-add-chat-ui` | Chat UI page |
| `prompt-028-github-actions-cicd` | CI/CD workflow |
| `prompt-029-unified-deployment-script` | deploy-all.ps1 |

See `prompts/prompt-order` for the full sequence.

---

## Quick Reference

### Cleanup Script (Before Rebuild)

```powershell
# Remove all agent-generated content
Remove-Item -Recurse -Force src, deploy-infra, deploy-app, tests -ErrorAction SilentlyContinue
Remove-Item -Force deploy-all.ps1, .deployment-context.json -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force .github/workflows -ErrorAction SilentlyContinue
```

### Common Bug Categories

| Symptom | Likely Fix Location |
|---------|---------------------|
| Wrong package version | `prompts/prompt-004-create-app-code` |
| Wrong Bicep config | `prompts/prompt-001-*` or `prompt-002-*` |
| CI/CD failure | `prompts/prompt-028-github-actions-cicd` |
| PowerShell syntax error | `.github/copilot-instructions.md` (pitfalls) |
| Column name mismatch | `prompts/prompt-004` or `.github/agents/dotnet-agent.md` |

---

## Questions?

If you're unsure whether a bug should be fixed in prompts vs guardrails:
- **Prompts** = "Build it this way" (positive instruction)
- **Guardrails** = "Never do this" (preventive rules)

When in doubt, add to both — prompts tell agents what to do, guardrails prevent them from doing the wrong thing.
