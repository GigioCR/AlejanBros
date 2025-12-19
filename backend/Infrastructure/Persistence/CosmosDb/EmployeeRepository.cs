using AlejanBros.Application.Mappings;
using AlejanBros.Configuration;
using AlejanBros.Domain.Entities;
using AlejanBros.Domain.Repositories;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlejanBros.Infrastructure.Persistence.CosmosDb;

public class EmployeeRepository : IEmployeeRepository
{
    private readonly Container _container;
    private readonly ILogger<EmployeeRepository> _logger;

    public EmployeeRepository(
        CosmosClient cosmosClient,
        IOptions<AzureSettings> settings,
        ILogger<EmployeeRepository> logger)
    {
        _logger = logger;
        var dbSettings = settings.Value.CosmosDb;
        var database = cosmosClient.GetDatabase(dbSettings.DatabaseName);
        _container = database.GetContainer(dbSettings.EmployeesContainer);
    }

    public async Task<Employee?> GetByIdAsync(string id)
    {
        try
        {
            var response = await _container.ReadItemAsync<Models.Employee>(id, new PartitionKey(id));
            return MapToDomain(response.Resource);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Employee with ID {Id} not found", id);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving employee {Id}", id);
            throw;
        }
    }

    public async Task<IEnumerable<Employee>> GetAllAsync()
    {
        try
        {
            var query = new QueryDefinition("SELECT * FROM c");
            var employees = new List<Employee>();

            using var iterator = _container.GetItemQueryIterator<Models.Employee>(query);
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                employees.AddRange(response.Select(MapToDomain));
            }

            return employees;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all employees");
            throw;
        }
    }

    public async Task<(IEnumerable<Employee> Items, int TotalCount)> GetPagedAsync(int page, int pageSize)
    {
        try
        {
            // Get total count
            var countQuery = new QueryDefinition("SELECT VALUE COUNT(1) FROM c");
            var countIterator = _container.GetItemQueryIterator<int>(countQuery);
            var countResponse = await countIterator.ReadNextAsync();
            var totalCount = countResponse.FirstOrDefault();

            // Get paginated items
            var offset = (page - 1) * pageSize;
            var query = new QueryDefinition("SELECT * FROM c ORDER BY c.name OFFSET @offset LIMIT @limit")
                .WithParameter("@offset", offset)
                .WithParameter("@limit", pageSize);

            var employees = new List<Employee>();
            using var iterator = _container.GetItemQueryIterator<Models.Employee>(query);
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                employees.AddRange(response.Select(MapToDomain));
            }

            return (employees, totalCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged employees");
            throw;
        }
    }

    public async Task<Employee> CreateAsync(Employee employee)
    {
        try
        {
            var model = MapToModel(employee);
            var response = await _container.CreateItemAsync(model, new PartitionKey(model.Id));
            _logger.LogInformation("Created employee {Name} with ID {Id}", employee.Name, employee.Id);
            return MapToDomain(response.Resource);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating employee {Name}", employee.Name);
            throw;
        }
    }

    public async Task<Employee> UpdateAsync(Employee employee)
    {
        try
        {
            var model = MapToModel(employee);
            var response = await _container.UpsertItemAsync(model, new PartitionKey(model.Id));
            _logger.LogInformation("Updated employee {Name} with ID {Id}", employee.Name, employee.Id);
            return MapToDomain(response.Resource);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating employee {Id}", employee.Id);
            throw;
        }
    }

    public async Task DeleteAsync(string id)
    {
        try
        {
            await _container.DeleteItemAsync<Models.Employee>(id, new PartitionKey(id));
            _logger.LogInformation("Deleted employee with ID {Id}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting employee {Id}", id);
            throw;
        }
    }

    public async Task<IEnumerable<Employee>> SearchBySkillsAsync(IEnumerable<string> skills)
    {
        try
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
            using var iterator = _container.GetItemQueryIterator<Models.Employee>(queryDefinition);
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                employees.AddRange(response.Select(MapToDomain));
            }

            return employees;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching employees by skills");
            throw;
        }
    }

    private Employee MapToDomain(Models.Employee model)
    {
        var skills = model.Skills.Select(s => 
            new Domain.ValueObjects.Skill(s.Name, (Domain.Enums.SkillLevel)s.Level, s.YearsUsed)).ToList();

        return Employee.Reconstruct(
            model.Id,
            model.Name,
            model.Email,
            model.Department,
            model.JobTitle,
            skills,
            model.YearsOfExperience,
            model.Certifications,
            (Domain.Enums.AvailabilityStatus)model.Availability,
            model.CurrentProjects,
            model.PreferredProjectTypes,
            model.Location,
            model.Bio,
            model.Embedding,
            model.CreatedAt,
            model.UpdatedAt
        );
    }

    private Models.Employee MapToModel(Employee domain)
    {
        return new Models.Employee
        {
            Id = domain.Id,
            Name = domain.Name,
            Email = domain.Email,
            Department = domain.Department,
            JobTitle = domain.JobTitle,
            Skills = domain.Skills.Select(s => new Models.Skill
            {
                Name = s.Name,
                Level = (Models.SkillLevel)s.Level,
                YearsUsed = s.YearsUsed
            }).ToList(),
            YearsOfExperience = domain.YearsOfExperience,
            Certifications = domain.Certifications.ToList(),
            Availability = (Models.AvailabilityStatus)domain.Availability,
            CurrentProjects = domain.CurrentProjects.ToList(),
            PreferredProjectTypes = domain.PreferredProjectTypes.ToList(),
            Location = domain.Location,
            Bio = domain.Bio,
            Embedding = domain.Embedding,
            CreatedAt = domain.CreatedAt,
            UpdatedAt = domain.UpdatedAt
        };
    }
}
