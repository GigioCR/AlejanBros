# AlejanBros (Employee Matcher)

Full-stack app for AI-powered employee project matching.

- **Backend**: Azure Functions (v4, .NET 8 isolated) exposing a REST API under `/api`.
- **Frontend**: React + TypeScript + Vite.

## Repo structure

```text
AlejanBros/
  backend/    # Azure Functions API
  frontend/   # React UI
```

## Prerequisites

- **Node.js** 18+ (recommended) + npm
- **.NET SDK** 8.x
- **Azure Functions Core Tools** v4 (to run Functions locally)
  - Verify: `func --version`

## Run the backend (Azure Functions)

### 1) Create `backend/local.settings.json`

`backend/local.settings.json` is **gitignored**. Start from:

- `backend/local.settings.template.json`

Steps:

1. Copy:
   - `backend/local.settings.template.json` → `backend/local.settings.json`
2. Fill in the values (Cosmos DB, Azure AI Search, Azure OpenAI, JWT secret).

Minimal template you can paste (adjust values as needed):

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",

    "CosmosDb__ConnectionString": "YOUR_COSMOS_DB_CONNECTION_STRING",
    "CosmosDb__DatabaseName": "EmployeeMatcherDB",
    "CosmosDb__EmployeesContainer": "Employees",
    "CosmosDb__ProjectsContainer": "Projects",
    "CosmosDb__UsersContainer": "Users",

    "Jwt__Secret": "YourSuperSecretKeyAtLeast32CharactersLong!",
    "Jwt__Issuer": "AlejanBros",
    "Jwt__ExpirationHours": "24",

    "Search__Endpoint": "https://YOUR_SEARCH_SERVICE.search.windows.net",
    "Search__ApiKey": "YOUR_SEARCH_API_KEY",
    "Search__IndexName": "employees-index",

    "OpenAI__Endpoint": "https://YOUR_OPENAI_SERVICE.openai.azure.com",
    "OpenAI__ApiKey": "YOUR_OPENAI_API_KEY",
    "OpenAI__EmbeddingDeployment": "text-embedding-ada-002",
    "OpenAI__ChatDeployment": "gpt-4o-mini"
  }
}
```

### 2) Start the Functions host

From `AlejanBros/backend`:

```bash
dotnet restore
dotnet build
func start
```

Backend base URL:

- `http://localhost:7071/api`

### 3) Initialize search + seed sample data (optional)

Once the backend is running:

PowerShell:

```powershell
Invoke-RestMethod -Method Post http://localhost:7071/api/admin/initialize-index
Invoke-RestMethod -Method Post http://localhost:7071/api/admin/seed
```

curl:

```bash
curl -X POST http://localhost:7071/api/admin/initialize-index
curl -X POST http://localhost:7071/api/admin/seed
```

## Run the frontend (Vite)

### 1) Install dependencies

From `AlejanBros/frontend`:

```bash
npm install
```

### 2) Start dev server

```bash
npm run dev
```

Frontend URL (Vite default):

- `http://localhost:5173`

### API proxy (important)

The frontend API client uses:

- `API_BASE = '/api'` (see `frontend/src/lib/api.ts`)

In dev, Vite proxies `/api` to the Functions host (see `frontend/vite.config.ts`):

- `/api/*` → `http://localhost:7071/api/*`

