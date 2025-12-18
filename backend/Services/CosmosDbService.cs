using AlejanBros.Configuration;
using AlejanBros.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlejanBros.Services;

public class CosmosDbService : ICosmosDbService
{
    private readonly CosmosClient _cosmosClient;
    private readonly Container _employeesContainer;
    private readonly Container _projectsContainer;
    private readonly ILogger<CosmosDbService> _logger;

    public CosmosDbService(
        CosmosClient cosmosClient,
        IOptions<AzureSettings> settings,
        ILogger<CosmosDbService> logger)
    {
        _cosmosClient = cosmosClient;
        _logger = logger;

        var dbSettings = settings.Value.CosmosDb;
        var database = _cosmosClient.GetDatabase(dbSettings.DatabaseName);
        _employeesContainer = database.GetContainer(dbSettings.EmployeesContainer);
        _projectsContainer = database.GetContainer(dbSettings.ProjectsContainer);
    }

    #region Employee Operations

    public async Task<Employee?> GetEmployeeAsync(string id)
    {
        try
        {
            var response = await _employeesContainer.ReadItemAsync<Employee>(id, new PartitionKey(id));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Employee with ID {Id} not found", id);
            return null;
        }
    }

    public async Task<IEnumerable<Employee>> GetAllEmployeesAsync()
    {
        var query = new QueryDefinition("SELECT * FROM c");
        var employees = new List<Employee>();

        using var iterator = _employeesContainer.GetItemQueryIterator<Employee>(query);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            employees.AddRange(response);
        }

        return employees;
    }

    public async Task<PaginatedResult<Employee>> GetEmployeesAsync(int page = 1, int pageSize = 10)
    {
        // Get total count
        var countQuery = new QueryDefinition("SELECT VALUE COUNT(1) FROM c");
        var countIterator = _employeesContainer.GetItemQueryIterator<int>(countQuery);
        var countResponse = await countIterator.ReadNextAsync();
        var totalCount = countResponse.FirstOrDefault();

        // Get paginated items with ORDER BY for consistent ordering
        var offset = (page - 1) * pageSize;
        var query = new QueryDefinition("SELECT * FROM c ORDER BY c.name OFFSET @offset LIMIT @limit")
            .WithParameter("@offset", offset)
            .WithParameter("@limit", pageSize);

        var employees = new List<Employee>();
        using var iterator = _employeesContainer.GetItemQueryIterator<Employee>(query);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            employees.AddRange(response);
        }

        return new PaginatedResult<Employee>
        {
            Items = employees,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<Employee> CreateEmployeeAsync(Employee employee)
    {
        employee.Id = string.IsNullOrEmpty(employee.Id) ? Guid.NewGuid().ToString() : employee.Id;
        employee.CreatedAt = DateTime.UtcNow;
        employee.UpdatedAt = DateTime.UtcNow;

        var response = await _employeesContainer.CreateItemAsync(employee, new PartitionKey(employee.Id));
        _logger.LogInformation("Created employee {Name} with ID {Id}", employee.Name, employee.Id);
        return response.Resource;
    }

    public async Task<Employee> UpdateEmployeeAsync(Employee employee)
    {
        employee.UpdatedAt = DateTime.UtcNow;
        var response = await _employeesContainer.UpsertItemAsync(employee, new PartitionKey(employee.Id));
        _logger.LogInformation("Updated employee {Name} with ID {Id}", employee.Name, employee.Id);
        return response.Resource;
    }

    public async Task DeleteEmployeeAsync(string id)
    {
        await _employeesContainer.DeleteItemAsync<Employee>(id, new PartitionKey(id));
        _logger.LogInformation("Deleted employee with ID {Id}", id);
    }

    public async Task<IEnumerable<Employee>> SearchEmployeesBySkillsAsync(IEnumerable<string> skills)
    {
        var skillsList = skills.ToList();
        if (!skillsList.Any())
            return Enumerable.Empty<Employee>();

        var skillsParam = string.Join(",", skillsList.Select((_, i) => $"@skill{i}"));
        var queryText = $"SELECT * FROM c WHERE EXISTS(SELECT VALUE s FROM s IN c.skills WHERE s.name IN ({skillsParam}))";

        var queryDefinition = new QueryDefinition(queryText);
        for (int i = 0; i < skillsList.Count; i++)
        {
            queryDefinition = queryDefinition.WithParameter($"@skill{i}", skillsList[i]);
        }

        var employees = new List<Employee>();
        using var iterator = _employeesContainer.GetItemQueryIterator<Employee>(queryDefinition);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            employees.AddRange(response);
        }

        return employees;
    }

    #endregion

    #region Project Operations

    public async Task<Project?> GetProjectAsync(string id)
    {
        try
        {
            var response = await _projectsContainer.ReadItemAsync<Project>(id, new PartitionKey(id));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Project with ID {Id} not found", id);
            return null;
        }
    }

    public async Task<PaginatedResult<Project>> GetProjectsAsync(int page = 1, int pageSize = 10)
    {
        // Get total count
        var countQuery = new QueryDefinition("SELECT VALUE COUNT(1) FROM c");
        var countIterator = _projectsContainer.GetItemQueryIterator<int>(countQuery);
        var countResponse = await countIterator.ReadNextAsync();
        var totalCount = countResponse.FirstOrDefault();

        // Get paginated items with ORDER BY for consistent ordering
        var offset = (page - 1) * pageSize;
        var query = new QueryDefinition("SELECT * FROM c ORDER BY c.name OFFSET @offset LIMIT @limit")
            .WithParameter("@offset", offset)
            .WithParameter("@limit", pageSize);

        var projects = new List<Project>();
        using var iterator = _projectsContainer.GetItemQueryIterator<Project>(query);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            projects.AddRange(response);
        }

        return new PaginatedResult<Project>
        {
            Items = projects,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<Project> CreateProjectAsync(Project project)
    {
        project.Id = string.IsNullOrEmpty(project.Id) ? Guid.NewGuid().ToString() : project.Id;
        project.CreatedAt = DateTime.UtcNow;

        var response = await _projectsContainer.CreateItemAsync(project, new PartitionKey(project.Id));
        _logger.LogInformation("Created project {Name} with ID {Id}", project.Name, project.Id);
        return response.Resource;
    }

    public async Task<Project> UpdateProjectAsync(Project project)
    {
        var response = await _projectsContainer.UpsertItemAsync(project, new PartitionKey(project.Id));
        _logger.LogInformation("Updated project {Name} with ID {Id}", project.Name, project.Id);
        return response.Resource;
    }

    public async Task DeleteProjectAsync(string id)
    {
        await _projectsContainer.DeleteItemAsync<Project>(id, new PartitionKey(id));
        _logger.LogInformation("Deleted project with ID {Id}", id);
    }

    #endregion
}
