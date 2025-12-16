# Employee-Project Matcher

An AI-powered solution for matching employees to projects based on skills, experience, and availability using Azure AI services.

## Architecture

```
┌─────────────────┐     ┌──────────────────┐     ┌─────────────────┐
│   Client/Chat   │────▶│  Azure Functions │────▶│  Azure OpenAI   │
│   (API/Swagger) │     │   (REST API)     │     │  (GPT + Embed)  │
└─────────────────┘     └────────┬─────────┘     └─────────────────┘
                                 │
                    ┌────────────┴────────────┐
                    ▼                         ▼
           ┌───────────────┐         ┌───────────────┐
           │ Azure Cosmos  │         │ Azure AI      │
           │ DB (Data)     │◀───────▶│ Search (RAG)  │
           └───────────────┘         └───────────────┘
```

## Features

- **Employee Management**: CRUD operations for employee profiles
- **Project Management**: CRUD operations for project definitions
- **AI-Powered Matching**: Uses Azure OpenAI to analyze and rank candidates
- **Semantic Search**: Vector search using embeddings for intelligent matching
- **Hybrid Search**: Combines keyword and semantic search for best results
- **Chat Interface**: Natural language queries for finding team members

## Tech Stack

- **.NET 8** - Azure Functions runtime
- **Azure Functions v4** - Serverless compute
- **Azure Cosmos DB** - NoSQL database for employees and projects
- **Azure AI Search** - Vector search with embeddings
- **Azure OpenAI** - GPT-4o-mini for analysis, text-embedding-ada-002 for embeddings

## API Endpoints

### Employees
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/employees` | Get all employees |
| GET | `/api/employees/{id}` | Get employee by ID |
| POST | `/api/employees` | Create new employee |
| PUT | `/api/employees/{id}` | Update employee |
| DELETE | `/api/employees/{id}` | Delete employee |

### Projects
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/projects` | Get all projects |
| GET | `/api/projects/{id}` | Get project by ID |
| POST | `/api/projects` | Create new project |
| PUT | `/api/projects/{id}` | Update project |
| DELETE | `/api/projects/{id}` | Delete project |

### Matching
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/match` | Find matching employees for requirements |
| GET | `/api/projects/{id}/matches` | Find matches for a specific project |
| POST | `/api/chat` | Natural language chat for finding employees |

### Admin
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/admin/initialize-index` | Initialize Azure AI Search index |
| POST | `/api/admin/reindex` | Reindex all employees |
| POST | `/api/admin/seed` | Seed sample data |
| GET | `/api/health` | Health check endpoint |

## Setup Instructions

### 1. Azure Resources Required

Create the following resources in Azure Portal:

1. **Resource Group**: `rg-employee-matcher`
2. **Azure Cosmos DB** (NoSQL, Serverless): `cosmos-employee-matcher`
   - Database: `EmployeeMatcherDB`
   - Containers: `Employees`, `Projects` (partition key: `/id`)
3. **Azure AI Search** (Free/Basic): `search-employee-matcher`
4. **Azure OpenAI**: `openai-employee-matcher`
   - Deploy models: `text-embedding-ada-002`, `gpt-4o-mini`

### 2. Local Development Setup

1. Copy `local.settings.template.json` to `local.settings.json`
2. Fill in your Azure credentials:

```json
{
  "Values": {
    "CosmosDb__ConnectionString": "YOUR_CONNECTION_STRING",
    "Search__Endpoint": "https://YOUR_SEARCH.search.windows.net",
    "Search__ApiKey": "YOUR_API_KEY",
    "OpenAI__Endpoint": "https://YOUR_OPENAI.openai.azure.com",
    "OpenAI__ApiKey": "YOUR_API_KEY"
  }
}
```

3. Install dependencies and run:

```bash
dotnet restore
dotnet build
func start
```

### 3. Initialize the System

After starting the functions, call these endpoints in order:

```bash
# 1. Initialize the search index
curl -X POST http://localhost:7071/api/admin/initialize-index

# 2. Seed sample data (employees and projects)
curl -X POST http://localhost:7071/api/admin/seed
```

## Usage Examples

### Find Matches with Natural Language

```bash
curl -X POST http://localhost:7071/api/chat \
  -H "Content-Type: application/json" \
  -d '{
    "message": "I need a team for a cloud migration project using Azure and C#. We need someone with DevOps experience and a frontend developer.",
    "teamSize": 4
  }'
```

### Find Matches with Specific Requirements

```bash
curl -X POST http://localhost:7071/api/match \
  -H "Content-Type: application/json" \
  -d '{
    "query": "Cloud migration project requiring microservices expertise",
    "requiredSkills": ["C#", "Azure", "Docker"],
    "techStack": ["Azure Functions", "Cosmos DB"],
    "minimumExperience": 3,
    "teamSize": 5,
    "availabilityRequired": true
  }'
```

### Get Matches for a Project

```bash
curl "http://localhost:7071/api/projects/{projectId}/matches?teamSize=4"
```

## Data Models

### Employee
```json
{
  "id": "string",
  "name": "string",
  "email": "string",
  "department": "string",
  "jobTitle": "string",
  "skills": [
    {
      "name": "string",
      "level": "Beginner|Intermediate|Advanced|Expert",
      "yearsUsed": 0
    }
  ],
  "yearsOfExperience": 0,
  "certifications": ["string"],
  "availability": "Available|PartiallyAvailable|Unavailable",
  "location": "string",
  "bio": "string"
}
```

### Project
```json
{
  "id": "string",
  "name": "string",
  "description": "string",
  "requiredSkills": [
    {
      "name": "string",
      "minimumLevel": "Beginner|Intermediate|Advanced|Expert",
      "required": true
    }
  ],
  "techStack": ["string"],
  "teamSize": 0,
  "duration": "string",
  "priority": "Low|Medium|High|Critical",
  "status": "Planning|Active|OnHold|Completed"
}
```

## Security Considerations

- API keys are stored in environment variables (use Azure Key Vault in production)
- Functions use `AuthorizationLevel.Function` (requires function key)
- Health endpoint is anonymous for monitoring
- No sensitive employee data (PII) is stored without anonymization

## Cost Optimization

- **Cosmos DB**: Serverless mode for pay-per-request
- **Azure Functions**: Consumption plan for pay-per-execution
- **AI Search**: Free tier available for development
- **OpenAI**: Use `gpt-4o-mini` instead of `gpt-4o` for lower costs

## Future Improvements

- Add authentication with Azure AD B2C
- Implement caching with Redis
- Add Application Insights for monitoring
- Create a web UI with React
- Add batch processing for large datasets
- Implement feedback loop for improving matches

## License

MIT
