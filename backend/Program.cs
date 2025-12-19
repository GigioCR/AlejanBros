using AlejanBros.Configuration;
using AlejanBros.Services;
using AlejanBros.Domain.Repositories;
using AlejanBros.Infrastructure.Persistence.CosmosDb;
using AlejanBros.Infrastructure.Search;
using AlejanBros.Infrastructure.AI.OpenAI;
using AlejanBros.Application.Interfaces;
using AlejanBros.Application.Services;
using Azure;
using Azure.AI.OpenAI;
using Azure.Search.Documents.Indexes;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Configure JWT Middleware
builder.UseMiddleware<AlejanBros.Middleware.JwtMiddleware>();

// Configure Azure Settings
builder.Services.Configure<AzureSettings>(options =>
{
    // Cosmos DB Settings
    options.CosmosDb.ConnectionString = Environment.GetEnvironmentVariable("CosmosDb__ConnectionString") ?? "";
    options.CosmosDb.DatabaseName = Environment.GetEnvironmentVariable("CosmosDb__DatabaseName") ?? "EmployeeMatcherDB";
    options.CosmosDb.EmployeesContainer = Environment.GetEnvironmentVariable("CosmosDb__EmployeesContainer") ?? "Employees";
    options.CosmosDb.ProjectsContainer = Environment.GetEnvironmentVariable("CosmosDb__ProjectsContainer") ?? "Projects";
    options.CosmosDb.UsersContainer = Environment.GetEnvironmentVariable("CosmosDb__UsersContainer") ?? "Users";

    // JWT Settings
    options.Jwt.Secret = Environment.GetEnvironmentVariable("Jwt__Secret") ?? "";
    options.Jwt.Issuer = Environment.GetEnvironmentVariable("Jwt__Issuer") ?? "AlejanBros";
    options.Jwt.ExpirationHours = int.TryParse(Environment.GetEnvironmentVariable("Jwt__ExpirationHours"), out var hours) ? hours : 24;

    // Azure AI Search Settings
    options.Search.Endpoint = Environment.GetEnvironmentVariable("Search__Endpoint") ?? "";
    options.Search.ApiKey = Environment.GetEnvironmentVariable("Search__ApiKey") ?? "";
    options.Search.IndexName = Environment.GetEnvironmentVariable("Search__IndexName") ?? "employees-index";

    // Azure OpenAI Settings
    options.OpenAI.Endpoint = Environment.GetEnvironmentVariable("OpenAI__Endpoint") ?? "";
    options.OpenAI.ApiKey = Environment.GetEnvironmentVariable("OpenAI__ApiKey") ?? "";
    options.OpenAI.EmbeddingDeployment = Environment.GetEnvironmentVariable("OpenAI__EmbeddingDeployment") ?? "text-embedding-ada-002";
    options.OpenAI.ChatDeployment = Environment.GetEnvironmentVariable("OpenAI__ChatDeployment") ?? "gpt-4o-mini";
});

// Register Azure Cosmos DB Client
builder.Services.AddSingleton(sp =>
{
    var connectionString = Environment.GetEnvironmentVariable("CosmosDb__ConnectionString");
    if (string.IsNullOrEmpty(connectionString))
    {
        throw new InvalidOperationException("CosmosDb__ConnectionString is not configured");
    }
    
    var options = new CosmosClientOptions
    {
        SerializerOptions = new CosmosSerializationOptions
        {
            PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
        }
    };
    
    return new CosmosClient(connectionString, options);
});

// Register Azure AI Search Client
builder.Services.AddSingleton(sp =>
{
    var endpoint = Environment.GetEnvironmentVariable("Search__Endpoint");
    var apiKey = Environment.GetEnvironmentVariable("Search__ApiKey");
    if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey))
    {
        throw new InvalidOperationException("Search__Endpoint and Search__ApiKey must be configured");
    }
    return new SearchIndexClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
});

// Register Azure OpenAI Client
builder.Services.AddSingleton(sp =>
{
    var endpoint = Environment.GetEnvironmentVariable("OpenAI__Endpoint");
    var apiKey = Environment.GetEnvironmentVariable("OpenAI__ApiKey");
    if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey))
    {
        throw new InvalidOperationException("OpenAI__Endpoint and OpenAI__ApiKey must be configured");
    }
    return new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
});

// Register Legacy Services (for backward compatibility)
builder.Services.AddSingleton<ICosmosDbService, CosmosDbService>();
builder.Services.AddSingleton<IOpenAIService, OpenAIService>();
builder.Services.AddSingleton<ISearchService, SearchService>();
builder.Services.AddSingleton<IEmployeeMatcherService, EmployeeMatcherService>();
builder.Services.AddSingleton<IAuthService, AuthService>();

// Register Clean Architecture - Domain Repositories
builder.Services.AddSingleton<IEmployeeRepository, EmployeeRepository>();
builder.Services.AddSingleton<IProjectRepository, ProjectRepository>();
builder.Services.AddSingleton<IUserRepository, UserRepository>();
builder.Services.AddSingleton<ISearchRepository, AzureAISearchRepository>();

// Register Clean Architecture - Application Services
builder.Services.AddSingleton<IEmbeddingService, EmbeddingService>();
builder.Services.AddSingleton<IEmployeeService, EmployeeService>();

builder.Build().Run();
