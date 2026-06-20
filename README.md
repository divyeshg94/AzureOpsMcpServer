# AzureOps MCP Server

An MCP (Model Context Protocol) server that exposes Azure resource operations as tools for GitHub Copilot and other MCP-compatible agents. Built with .NET 10 and the Azure Resource Manager SDK. Authentication uses `DefaultAzureCredential` — `az login` locally, system-assigned Managed Identity in Azure.

Part of the **MCP on Azure: From Zero to Production** series on [Microsoft Azure in Practice](https://medium.com/microsoft-azure).

---

## Blog Series

Each branch in this repo corresponds to a published article. Follow the series to build this server from scratch.

| Part | Article | Branch | What's covered |
|---|---|---|---|
| 1 | [Why MCP Matters for Azure](https://medium.com/gitconnected/what-is-mcp-and-why-should-every-azure-developer-care-3d65e8eff76c) | — | Mental model, MCP protocol, why agents need structured tools |
| 2 | [First MCP Server in C# .NET](https://medium.com/gitconnected/your-first-mcp-server-in-c-net-build-something-real-in-30-minutes-ea146c8226e2) | [`part2`](https://github.com/divyeshg94/AzureOpsMcpServer/tree/part2) | Server scaffold, three tools with simulated data, Copilot integration |
| 3 | From Local to Azure: Deploy to Production | [`part3`](https://github.com/divyeshg94/AzureOpsMcpServer/tree/part3) | Real Azure SDK calls, local validation, Container Apps deployment |
| 4 | Secure with Entra ID + APIM | — | Coming soon |

---

## Tools

| Tool | Description |
|---|---|
| `get_deployment_status` | Recent deployment history for a resource group — name, status, timestamp, duration, triggered-by |
| `get_resource_tags` | FinOps tag compliance check — identifies missing `Environment`, `Owner`, `CostCenter`, `Application` tags |
| `check_resource_health` | Azure Resource Health state for a resource group — Available, Degraded, Unavailable, Unknown |

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli)
- Azure subscription with at least **Reader** and **Resource Health Reader** roles on the target scope

---

## Local Development

```powershell
# Authenticate
az login

# Run — binds to http://localhost:5100
dotnet run

# Health check
Invoke-RestMethod http://localhost:5100/health
```

### Required RBAC (local)

```powershell
$MY_ID = az ad signed-in-user show --query id -o tsv

az role assignment create --role "Reader" `
  --assignee $MY_ID `
  --scope "/subscriptions/{your-sub-id}/resourceGroups/{your-rg}"

az role assignment create --role "Resource Health Reader" `
  --assignee $MY_ID `
  --scope "/subscriptions/{your-sub-id}"
```

---

## GitHub Copilot Integration

`.vscode/mcp.json` is pre-configured. Open Copilot Chat (`Ctrl+Alt+I`), switch to Agent mode, confirm `azureops-mcp` appears in the Tools panel.

Example prompts:
```
Get the last 5 deployments for subscription {sub-id}, resource group {rg}
Check FinOps tag compliance for subscription {sub-id}, resource group {rg}
Check the health of resource group {rg} in subscription {sub-id}
```

---

## Deploy to Azure Container Apps

### 1. Set variables

```powershell
$RG       = "rg-azureops-mcp"
$LOCATION = "eastus"
$ACR      = "azureopsmcpacr"
$ACA_ENV  = "azureops-mcp-env"
$ACA_APP  = "azureops-mcp-server"
$SUB_ID   = az account show --query id -o tsv
```

### 2. Create infrastructure

```powershell
az group create --name $RG --location $LOCATION

az acr create --resource-group $RG --name $ACR --sku Basic --admin-enabled false

az containerapp env create --name $ACA_ENV --resource-group $RG --location $LOCATION
```

### 3. Build and push image

```powershell
az acr build --registry $ACR --image azureops-mcp-server:latest .
```

### 4. Deploy with Managed Identity

```powershell
# Enable admin for bootstrap pull (disabled again after step 6)
az acr update --name $ACR --admin-enabled true
$ACR_PASS = az acr credential show --name $ACR --query "passwords[0].value" -o tsv

az containerapp create `
  --name $ACA_APP --resource-group $RG --environment $ACA_ENV `
  --image "$($ACR).azurecr.io/azureops-mcp-server:latest" `
  --target-port 8080 --ingress external `
  --min-replicas 1 --max-replicas 10 `
  --cpu 0.25 --memory 0.5Gi `
  --system-assigned `
  --registry-server "$($ACR).azurecr.io" `
  --registry-username $ACR --registry-password $ACR_PASS

$PRINCIPAL_ID = az containerapp identity show `
  --name $ACA_APP --resource-group $RG --query principalId -o tsv

$ACR_ID = az acr show --name $ACR --query id -o tsv
az role assignment create --role AcrPull `
  --assignee-object-id $PRINCIPAL_ID --assignee-principal-type ServicePrincipal `
  --scope $ACR_ID

az containerapp registry set --name $ACA_APP --resource-group $RG `
  --server "$($ACR).azurecr.io" --identity system

az acr update --name $ACR --admin-enabled false

az role assignment create --role "Reader" `
  --assignee-object-id $PRINCIPAL_ID --assignee-principal-type ServicePrincipal `
  --scope "/subscriptions/$SUB_ID"

az role assignment create --role "Resource Health Reader" `
  --assignee-object-id $PRINCIPAL_ID --assignee-principal-type ServicePrincipal `
  --scope "/subscriptions/$SUB_ID"
```

### 5. Get endpoint and validate

```powershell
$FQDN = az containerapp show `
  --name $ACA_APP --resource-group $RG `
  --query "properties.configuration.ingress.fqdn" -o tsv

Invoke-RestMethod "https://$FQDN/health"
```

Update `.vscode/mcp.json` to point at `https://$FQDN/mcp` and reload VS Code.

---

## Project Structure

```
AzureOpsMcpServer/
├── .vscode/
│   └── mcp.json                  ← MCP endpoint config for VS Code Copilot
├── Models/
│   └── Models.cs                 ← Strongly-typed result records
├── Prompts/
│   └── IncidentPrompt.cs         ← Incident triage prompt
├── Properties/
│   └── launchSettings.json       ← Local port (5100)
├── Resources/
│   └── RunbookResource.cs        ← Runbook resource
├── Tools/
│   └── AzureOpsTools.cs          ← All three MCP tools
├── .dockerignore
├── AzureOpsMcpServer.csproj
├── Dockerfile
└── Program.cs
```

---

## Key Design Decisions

- **`DefaultAzureCredential`** — no secrets, no config changes between local and production
- **`app.Run()` with no URL** — reads `ASPNETCORE_HTTP_PORTS` from environment; `launchSettings.json` sets 5100 locally, Dockerfile sets 8080 in container
- **Stateless HTTP transport** — compatible with ACA scale-out; no sticky sessions required
- **`--assignee-object-id` for RBAC** — `--assignee` silently misresolves for managed identity GUIDs; use explicit object ID + principal type
