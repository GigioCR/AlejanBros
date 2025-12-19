using AlejanBros.Configuration;
using AlejanBros.Domain.Entities;
using AlejanBros.Domain.Repositories;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlejanBros.Infrastructure.Persistence.CosmosDb;

public class ProjectRepository : IProjectRepository
{
    private readonly Container _container;
    private readonly ILogger<ProjectRepository> _logger;

    public ProjectRepository(
        CosmosClient cosmosClient,
        IOptions<AzureSettings> settings,
        ILogger<ProjectRepository> logger)
    {
        _logger = logger;
        var dbSettings = settings.Value.CosmosDb;
        var database = cosmosClient.GetDatabase(dbSettings.DatabaseName);
        _container = database.GetContainer(dbSettings.ProjectsContainer);
    }

    public async Task<Project?> GetByIdAsync(string id)
    {
        try
        {
            var response = await _container.ReadItemAsync<Models.Project>(id, new PartitionKey(id));
            return MapToDomain(response.Resource);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Project with ID {Id} not found", id);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving project {Id}", id);
            throw;
        }
    }

    public async Task<(IEnumerable<Project> Items, int TotalCount)> GetPagedAsync(int page, int pageSize)
    {
        try
        {
            var countQuery = new QueryDefinition("SELECT VALUE COUNT(1) FROM c");
            var countIterator = _container.GetItemQueryIterator<int>(countQuery);
            var countResponse = await countIterator.ReadNextAsync();
            var totalCount = countResponse.FirstOrDefault();

            var offset = (page - 1) * pageSize;
            var query = new QueryDefinition("SELECT * FROM c ORDER BY c.name OFFSET @offset LIMIT @limit")
                .WithParameter("@offset", offset)
                .WithParameter("@limit", pageSize);

            var projects = new List<Project>();
            using var iterator = _container.GetItemQueryIterator<Models.Project>(query);
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                projects.AddRange(response.Select(MapToDomain));
            }

            return (projects, totalCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged projects");
            throw;
        }
    }

    public async Task<Project> CreateAsync(Project project)
    {
        try
        {
            var model = MapToModel(project);
            var response = await _container.CreateItemAsync(model, new PartitionKey(model.Id));
            _logger.LogInformation("Created project {Name} with ID {Id}", project.Name, project.Id);
            return MapToDomain(response.Resource);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating project {Name}", project.Name);
            throw;
        }
    }

    public async Task<Project> UpdateAsync(Project project)
    {
        try
        {
            var model = MapToModel(project);
            var response = await _container.UpsertItemAsync(model, new PartitionKey(model.Id));
            _logger.LogInformation("Updated project {Name} with ID {Id}", project.Name, project.Id);
            return MapToDomain(response.Resource);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating project {Id}", project.Id);
            throw;
        }
    }

    public async Task DeleteAsync(string id)
    {
        try
        {
            await _container.DeleteItemAsync<Models.Project>(id, new PartitionKey(id));
            _logger.LogInformation("Deleted project with ID {Id}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting project {Id}", id);
            throw;
        }
    }

    private Project MapToDomain(Models.Project model)
    {
        var requiredSkills = model.RequiredSkills.Select(s =>
            new Domain.ValueObjects.RequiredSkill(s.Name, (Domain.Enums.SkillLevel)s.MinimumLevel, s.Required)).ToList();

        return Project.Reconstruct(
            model.Id,
            model.Name,
            model.Description,
            requiredSkills,
            model.TechStack,
            model.TeamSize,
            model.Duration,
            model.StartDate,
            (Domain.Enums.ProjectPriority)model.Priority,
            model.ProjectType,
            model.Client,
            (Domain.Enums.ProjectStatus)model.Status,
            model.CreatedAt
        );
    }

    private Models.Project MapToModel(Project domain)
    {
        return new Models.Project
        {
            Id = domain.Id,
            Name = domain.Name,
            Description = domain.Description,
            RequiredSkills = domain.RequiredSkills.Select(s => new Models.RequiredSkill
            {
                Name = s.Name,
                MinimumLevel = (Models.SkillLevel)s.MinimumLevel,
                Required = s.Required
            }).ToList(),
            TechStack = domain.TechStack.ToList(),
            TeamSize = domain.TeamSize,
            Duration = domain.Duration,
            StartDate = domain.StartDate,
            Priority = (Models.ProjectPriority)domain.Priority,
            ProjectType = domain.ProjectType,
            Client = domain.Client,
            Status = (Models.ProjectStatus)domain.Status,
            CreatedAt = domain.CreatedAt
        };
    }
}
