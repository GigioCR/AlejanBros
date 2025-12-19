using AlejanBros.Configuration;
using AlejanBros.Domain.Repositories;
using AlejanBros.Models;
using AlejanBros.Services;
using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlejanBros.Infrastructure.Search;

public class AzureAISearchRepository : ISearchRepository
{
    private readonly SearchIndexClient _indexClient;
    private readonly SearchClient _searchClient;
    private readonly ICosmosDbService _cosmosDbService;
    private readonly string _indexName;
    private readonly ILogger<AzureAISearchRepository> _logger;

    public AzureAISearchRepository(
        SearchIndexClient indexClient,
        ICosmosDbService cosmosDbService,
        IOptions<AzureSettings> settings,
        ILogger<AzureAISearchRepository> logger)
    {
        _indexClient = indexClient;
        _cosmosDbService = cosmosDbService;
        _indexName = settings.Value.Search.IndexName;
        _searchClient = _indexClient.GetSearchClient(_indexName);
        _logger = logger;
    }

    public async Task InitializeIndexAsync()
    {
        try
        {
            var fieldBuilder = new FieldBuilder();
            var searchFields = fieldBuilder.Build(typeof(EmployeeSearchDocument));

            var definition = new SearchIndex(_indexName, searchFields);
            await _indexClient.CreateOrUpdateIndexAsync(definition);
            _logger.LogInformation("Search index {IndexName} initialized", _indexName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing search index");
            throw;
        }
    }

    public async Task<IEnumerable<string>> HybridSearchAsync(string query, float[] embedding, int top = 10)
    {
        try
        {
            var searchOptions = new SearchOptions
            {
                Size = top,
                Select = { "id" }
            };

            var vectorQuery = new VectorizedQuery(embedding)
            {
                KNearestNeighborsCount = top,
                Fields = { "embedding" }
            };

            searchOptions.VectorSearch = new();
            searchOptions.VectorSearch.Queries.Add(vectorQuery);

            var response = await _searchClient.SearchAsync<EmployeeSearchDocument>(query, searchOptions);
            var employeeIds = new List<string>();

            await foreach (var result in response.Value.GetResultsAsync())
            {
                employeeIds.Add(result.Document.Id);
            }

            return employeeIds;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing hybrid search");
            throw;
        }
    }

    public async Task<IEnumerable<Domain.Repositories.SearchDocument>> SearchAsync(string query, int top = 10)
    {
        try
        {
            var searchOptions = new SearchOptions { Size = top };
            var response = await _searchClient.SearchAsync<EmployeeSearchDocument>(query, searchOptions);
            var results = new List<Domain.Repositories.SearchDocument>();

            await foreach (var result in response.Value.GetResultsAsync())
            {
                results.Add(MapToSearchDocument(result.Document));
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing text search");
            throw;
        }
    }

    public async Task<IEnumerable<Domain.Repositories.SearchDocument>> VectorSearchAsync(float[] embedding, int top = 10)
    {
        try
        {
            var searchOptions = new SearchOptions { Size = top };
            var vectorQuery = new VectorizedQuery(embedding)
            {
                KNearestNeighborsCount = top,
                Fields = { "embedding" }
            };

            searchOptions.VectorSearch = new();
            searchOptions.VectorSearch.Queries.Add(vectorQuery);

            var response = await _searchClient.SearchAsync<EmployeeSearchDocument>(null, searchOptions);
            var results = new List<Domain.Repositories.SearchDocument>();

            await foreach (var result in response.Value.GetResultsAsync())
            {
                results.Add(MapToSearchDocument(result.Document));
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing vector search");
            throw;
        }
    }

    public async Task IndexEmployeesAsync(IEnumerable<string> employeeIds)
    {
        foreach (var id in employeeIds)
        {
            await IndexEmployeeAsync(id);
        }
    }

    public async Task IndexEmployeeAsync(string employeeId)
    {
        try
        {
            var employee = await _cosmosDbService.GetEmployeeAsync(employeeId);
            if (employee == null)
            {
                _logger.LogWarning("Employee {Id} not found for indexing", employeeId);
                return;
            }

            var searchDoc = new EmployeeSearchDocument
            {
                Id = employee.Id,
                Name = employee.Name,
                Department = employee.Department,
                JobTitle = employee.JobTitle,
                Skills = employee.Skills.Select(s => s.Name).ToArray(),
                YearsOfExperience = employee.YearsOfExperience,
                Certifications = employee.Certifications.ToArray(),
                Availability = employee.Availability.ToString(),
                Location = employee.Location,
                Bio = employee.Bio,
                Embedding = employee.Embedding
            };

            await _searchClient.IndexDocumentsAsync(IndexDocumentsBatch.Upload(new[] { searchDoc }));
            _logger.LogInformation("Indexed employee {Id}", employeeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error indexing employee {Id}", employeeId);
            throw;
        }
    }

    public async Task DeleteEmployeeFromIndexAsync(string employeeId)
    {
        try
        {
            await _searchClient.DeleteDocumentsAsync("id", new[] { employeeId });
            _logger.LogInformation("Deleted employee {Id} from index", employeeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting employee {Id} from index", employeeId);
            throw;
        }
    }

    public async Task RebuildIndexAsync()
    {
        try
        {
            var employees = await _cosmosDbService.GetAllEmployeesAsync();
            var searchDocs = employees.Select(e => new EmployeeSearchDocument
            {
                Id = e.Id,
                Name = e.Name,
                Department = e.Department,
                JobTitle = e.JobTitle,
                Skills = e.Skills.Select(s => s.Name).ToArray(),
                YearsOfExperience = e.YearsOfExperience,
                Certifications = e.Certifications.ToArray(),
                Availability = e.Availability.ToString(),
                Location = e.Location,
                Bio = e.Bio,
                Embedding = e.Embedding
            }).ToList();

            if (searchDocs.Any())
            {
                await _searchClient.IndexDocumentsAsync(IndexDocumentsBatch.Upload(searchDocs));
                _logger.LogInformation("Rebuilt search index with {Count} employees", searchDocs.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rebuilding search index");
            throw;
        }
    }

    private Domain.Repositories.SearchDocument MapToSearchDocument(EmployeeSearchDocument doc)
    {
        return new Domain.Repositories.SearchDocument
        {
            Id = doc.Id,
            Name = doc.Name,
            Department = doc.Department,
            JobTitle = doc.JobTitle,
            Skills = doc.Skills,
            YearsOfExperience = doc.YearsOfExperience,
            Certifications = doc.Certifications,
            Availability = doc.Availability,
            Location = doc.Location,
            Bio = doc.Bio,
            Embedding = doc.Embedding
        };
    }
}
