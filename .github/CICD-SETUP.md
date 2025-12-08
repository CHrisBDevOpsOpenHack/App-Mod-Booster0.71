## CI/CD Setup (GitHub Actions with OIDC)

1. Create a service principal with OIDC federation:
   ```powershell
   az ad sp create-for-rbac --name "app-mod-expense-sp" --role Contributor --scopes /subscriptions/<subscription-id>
   ```
2. Grant **User Access Administrator** at the subscription level so role assignments work:
   ```powershell
   az role assignment create --assignee <sp-app-id> --role "User Access Administrator" --scope /subscriptions/<subscription-id>
   ```
3. Add federated credentials for your repository:
   - Audience: `api://AzureADTokenExchange`
   - Subject identifier: `repo:<org>/<repo>:ref:refs/heads/main`
4. Configure GitHub repository variables (Actions → Variables):
   - `AZURE_CLIENT_ID`
   - `AZURE_TENANT_ID`
   - `AZURE_SUBSCRIPTION_ID`
5. Trigger the workflow manually from **Actions → Deploy to Azure**.

Notes:
- The workflow installs go-sqlcmd from GitHub releases (Ubuntu 24.04 compatible).
- It calls the same PowerShell scripts used locally: `deploy-infra/deploy.ps1` then `deploy-app/deploy.ps1`.
- A 60-second delay between infra and app deploy avoids SCM restart conflicts.
