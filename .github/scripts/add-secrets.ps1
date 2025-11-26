# add-github-secrets.ps1 - Add GitHub secrets for Azure OIDC authentication
# This script uses GitHub CLI to add the required secrets to the repository
#
# Prerequisites:
# - GitHub CLI installed (gh)
# - Authenticated with GitHub (gh auth login)
#
# Usage: .\add-github-secrets.ps1

# Check GitHub authentication
Write-Host "Checking GitHub authentication..." -ForegroundColor Yellow
$authStatus = gh auth status 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Not authenticated with GitHub" -ForegroundColor Red
    Write-Host "Please run: gh auth login" -ForegroundColor Yellow
    exit 1
}
Write-Host "✓ Authenticated with GitHub" -ForegroundColor Green
Write-Host ""

# Configuration - Get repo from git remote
$gitRemote = git remote get-url origin
$GITHUB_REPO = $gitRemote -replace '^https://github.com/', '' -replace '\.git$', ''

# Paste your values here:
$ClientId = "2c55118c-c315-4074-a250-xxxx"
$TenantId = "95fdb808-f6f3-4abc-9cb2-xxxx"
$SubscriptionId = "bf0ff2fe-5503-48b0-8b52-xxxx"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Add GitHub Secrets" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Repository: $GITHUB_REPO" -ForegroundColor Gray
Write-Host ""

# Add AZURE_CLIENT_ID
Write-Host "Adding AZURE_CLIENT_ID..." -ForegroundColor Yellow
$ClientId | gh secret set AZURE_CLIENT_ID --repo $GITHUB_REPO

# Add AZURE_TENANT_ID
Write-Host ""
Write-Host "Adding AZURE_TENANT_ID..." -ForegroundColor Yellow
$TenantId | gh secret set AZURE_TENANT_ID --repo $GITHUB_REPO

# Add AZURE_SUBSCRIPTION_ID
Write-Host ""
Write-Host "Adding AZURE_SUBSCRIPTION_ID..." -ForegroundColor Yellow
$SubscriptionId | gh secret set AZURE_SUBSCRIPTION_ID --repo $GITHUB_REPO

Write-Host ""
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Secrets Added Successfully!" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Verifying secrets..." -ForegroundColor Yellow
gh secret list --repo $GITHUB_REPO
Write-Host ""
Write-Host "You can now run the GitHub Actions workflow." -ForegroundColor Green
Write-Host ""
