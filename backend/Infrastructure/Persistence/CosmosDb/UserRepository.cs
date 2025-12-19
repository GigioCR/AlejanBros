using AlejanBros.Configuration;
using AlejanBros.Domain.Entities;
using AlejanBros.Domain.Repositories;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlejanBros.Infrastructure.Persistence.CosmosDb;

public class UserRepository : IUserRepository
{
    private readonly Container _container;
    private readonly ILogger<UserRepository> _logger;

    public UserRepository(
        CosmosClient cosmosClient,
        IOptions<AzureSettings> settings,
        ILogger<UserRepository> logger)
    {
        _logger = logger;
        var dbSettings = settings.Value.CosmosDb;
        var database = cosmosClient.GetDatabase(dbSettings.DatabaseName);
        _container = database.GetContainer(dbSettings.UsersContainer);
    }

    public async Task<Domain.Entities.User?> GetByIdAsync(string id)
    {
        try
        {
            var response = await _container.ReadItemAsync<Models.User>(id, new PartitionKey(id));
            return MapToDomain(response.Resource);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user {Id}", id);
            throw;
        }
    }

    public async Task<Domain.Entities.User?> GetByEmailAsync(string email)
    {
        try
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.email = @email")
                .WithParameter("@email", email);

            using var iterator = _container.GetItemQueryIterator<Models.User>(query);
            if (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                var user = response.FirstOrDefault();
                return user != null ? MapToDomain(user) : null;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user by email {Email}", email);
            throw;
        }
    }

    public async Task<Domain.Entities.User> CreateAsync(Domain.Entities.User user)
    {
        try
        {
            var model = MapToModel(user);
            var response = await _container.CreateItemAsync(model, new PartitionKey(model.Id));
            _logger.LogInformation("Created user {Email} with ID {Id}", user.Email, user.Id);
            return MapToDomain(response.Resource);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user {Email}", user.Email);
            throw;
        }
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        try
        {
            var query = new QueryDefinition("SELECT VALUE COUNT(1) FROM c WHERE c.email = @email")
                .WithParameter("@email", email);

            using var iterator = _container.GetItemQueryIterator<int>(query);
            if (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                return response.FirstOrDefault() > 0;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if email exists {Email}", email);
            throw;
        }
    }

    private Domain.Entities.User MapToDomain(Models.User model)
    {
        return Domain.Entities.User.Reconstruct(
            model.Id,
            model.Email,
            model.Name,
            model.PasswordHash,
            model.Role,
            model.CreatedAt
        );
    }

    private Models.User MapToModel(Domain.Entities.User domain)
    {
        return new Models.User
        {
            Id = domain.Id,
            Email = domain.Email,
            Name = domain.Name,
            PasswordHash = domain.PasswordHash,
            Role = domain.Role,
            CreatedAt = domain.CreatedAt
        };
    }
}
