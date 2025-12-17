namespace AlejanBros.Configuration;

public class AzureSettings
{
    public CosmosDbSettings CosmosDb { get; set; } = new();
    public SearchSettings Search { get; set; } = new();
    public OpenAISettings OpenAI { get; set; } = new();
    public JwtSettings Jwt { get; set; } = new();
}

public class CosmosDbSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = "EmployeeMatcherDB";
    public string EmployeesContainer { get; set; } = "Employees";
    public string ProjectsContainer { get; set; } = "Projects";
    public string UsersContainer { get; set; } = "Users";
}

public class SearchSettings
{
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string IndexName { get; set; } = "employees-index";
}

public class OpenAISettings
{
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string EmbeddingDeployment { get; set; } = "text-embedding-ada-002";
    public string ChatDeployment { get; set; } = "gpt-4o-mini";
}

public class JwtSettings
{
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "AlejanBros";
    public int ExpirationHours { get; set; } = 24;
}
