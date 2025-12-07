# CI/CD Setup Guide

This guide explains how to set up automated deployments using GitHub Actions with OIDC authentication.

## Overview

The GitHub Actions workflow (`.github/workflows/deploy.yml`) automates deployment of both infrastructure and application code to Azure using OpenID Connect (OIDC) for secure, passwordless authentication.

## Prerequisites

- Azure subscription with Contributor + User Access Administrator roles
- GitHub repository
- PowerShell 7+ for setup commands

## Quick Setup

### 1. Create Service Principal

```powershell
$subscriptionId = "your-subscription-id"
$appName = "gh-expensemgmt-deploy"
$repoOwner = "your-github-org"
$repoName = "your-repo-name"

az login
az account set --subscription $subscriptionId

$sp = az ad sp create-for-rbac --name $appName --role Contributor --scopes "/subscriptions/$subscriptionId" | ConvertFrom-Json
$clientId = $sp.appId
```

### 2. Assign User Access Administrator Role

```powershell
az role assignment create --assignee $clientId --role "User Access Administrator" --scope "/subscriptions/$subscriptionId"
```

### 3. Create Federated Credentials

```powershell
az ad app federated-credential create --id $clientId --parameters '{
  "name": "github-main",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "repo:'$repoOwner'/'$repoName':ref:refs/heads/main",
  "audiences": ["api://AzureADTokenExchange"]
}'
```

### 4. Configure GitHub

In your repository: Settings → Secrets and variables → Actions → Variables

Create these variables:
- `AZURE_CLIENT_ID`: Service Principal Client ID
- `AZURE_TENANT_ID`: Your Azure Tenant ID
- `AZURE_SUBSCRIPTION_ID`: Your Subscription ID

### 5. Create Environment

Settings → Environments → New environment → Name it `production`

## Running Deployments

Actions → Deploy to Azure → Run workflow

See full documentation for troubleshooting and advanced configuration.
