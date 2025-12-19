using AlejanBros.Configuration;
using AlejanBros.Models;
using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlejanBros.Services;

public class SearchService : ISearchService
{
    private readonly SearchIndexClient _indexClient;
    private readonly SearchClient _searchClient;
    private readonly IOpenAIService _openAIService;
    private readonly SearchSettings _settings;
    private readonly ILogger<SearchService> _logger;

    public SearchService(
        SearchIndexClient indexClient,
        IOpenAIService openAIService,
        IOptions<AzureSettings> settings,
        ILogger<SearchService> logger)
    {
        _indexClient = indexClient;
        _openAIService = openAIService;
        _settings = settings.Value.Search;
        _logger = logger;

        _searchClient = new SearchClient(
            new Uri(_settings.Endpoint),
            _settings.IndexName,
            new AzureKeyCredential(_settings.ApiKey));
    }

    public async Task InitializeIndexAsync()
    {
        try
        {
            var vectorSearch = new VectorSearch();
            vectorSearch.Profiles.Add(new VectorSearchProfile("vector-profile", "vector-algorithm"));
            vectorSearch.Algorithms.Add(new HnswAlgorithmConfiguration("vector-algorithm"));

            var index = new SearchIndex(_settings.IndexName)
            {
                VectorSearch = vectorSearch,
                Fields = new FieldBuilder().Build(typeof(EmployeeSearchDocument))
            };

            await _indexClient.CreateOrUpdateIndexAsync(index);
            _logger.LogInformation("Search index '{IndexName}' created/updated successfully", _settings.IndexName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize search index");
            throw;
        }
    }

    public async Task IndexEmployeeAsync(Employee employee)
    {
        var document = await CreateSearchDocumentAsync(employee);
        var batch = IndexDocumentsBatch.Upload(new[] { document });
        await _searchClient.IndexDocumentsAsync(batch);
        _logger.LogInformation("Indexed employee {Name} with ID {Id}", employee.Name, employee.Id);
    }

    public async Task IndexEmployeesAsync(IEnumerable<Employee> employees)
    {
        var documents = new List<EmployeeSearchDocument>();
        foreach (var employee in employees)
        {
            var document = await CreateSearchDocumentAsync(employee);
            documents.Add(document);
        }

        if (documents.Any())
        {
            var batch = IndexDocumentsBatch.Upload(documents);
            await _searchClient.IndexDocumentsAsync(batch);
            _logger.LogInformation("Indexed {Count} employees", documents.Count);
        }
    }

    public async Task DeleteEmployeeFromIndexAsync(string employeeId)
    {
        var batch = IndexDocumentsBatch.Delete("id", new[] { employeeId });
        await _searchClient.IndexDocumentsAsync(batch);
        _logger.LogInformation("Removed employee {Id} from search index", employeeId);
    }

    public async Task ClearAndRebuildIndexAsync(IEnumerable<Employee> employees)
    {
        _logger.LogInformation("Clearing and rebuilding search index...");
        
        // Delete the index and recreate it
        try
        {
            await _indexClient.DeleteIndexAsync(_settings.IndexName);
            _logger.LogInformation("Deleted existing index '{IndexName}'", _settings.IndexName);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogInformation("Index '{IndexName}' did not exist, creating new one", _settings.IndexName);
        }
        
        // Recreate the index
        await InitializeIndexAsync();
        
        // Re-index all employees
        await IndexEmployeesAsync(employees);
        
        _logger.LogInformation("Search index rebuilt successfully with {Count} employees", employees.Count());
    }

    public async Task<IEnumerable<EmployeeSearchDocument>> SearchAsync(string query, int top = 10)
    {
        var options = new SearchOptions
        {
            Size = top,
            IncludeTotalCount = true,
            Select = { "id", "name", "department", "jobTitle", "skills", "yearsOfExperience", "certifications", "availability", "location", "bio" }
        };

        var response = await _searchClient.SearchAsync<EmployeeSearchDocument>(query, options);
        var results = new List<EmployeeSearchDocument>();

        await foreach (var result in response.Value.GetResultsAsync())
        {
            results.Add(result.Document);
        }

        _logger.LogInformation("Text search for '{Query}' returned {Count} results", query, results.Count);
        return results;
    }

    public async Task<IEnumerable<EmployeeSearchDocument>> VectorSearchAsync(float[] embedding, int top = 10)
    {
        var vectorQuery = new VectorizedQuery(embedding)
        {
            KNearestNeighborsCount = top,
            Fields = { "embedding" }
        };

        var options = new SearchOptions
        {
            VectorSearch = new VectorSearchOptions
            {
                Queries = { vectorQuery }
            },
            Size = top,
            Select = { "id", "name", "department", "jobTitle", "skills", "yearsOfExperience", "certifications", "availability", "location", "bio" }
        };

        var response = await _searchClient.SearchAsync<EmployeeSearchDocument>(null, options);
        var results = new List<EmployeeSearchDocument>();

        await foreach (var result in response.Value.GetResultsAsync())
        {
            results.Add(result.Document);
        }

        _logger.LogInformation("Vector search returned {Count} results", results.Count);
        return results;
    }

    public async Task<IEnumerable<EmployeeSearchDocument>> HybridSearchAsync(string query, float[] embedding, int top = 10)
    {
        var vectorQuery = new VectorizedQuery(embedding)
        {
            KNearestNeighborsCount = top,
            Fields = { "embedding" }
        };

        var options = new SearchOptions
        {
            VectorSearch = new VectorSearchOptions
            {
                Queries = { vectorQuery }
            },
            Size = top,
            Select = { "id", "name", "department", "jobTitle", "skills", "yearsOfExperience", "certifications", "availability", "location", "bio" }
        };

        var response = await _searchClient.SearchAsync<EmployeeSearchDocument>(query, options);
        var results = new List<EmployeeSearchDocument>();

        await foreach (var result in response.Value.GetResultsAsync())
        {
            results.Add(result.Document);
        }

        _logger.LogInformation("Hybrid search for '{Query}' returned {Count} results", query, results.Count);
        return results;
    }

    private async Task<EmployeeSearchDocument> CreateSearchDocumentAsync(Employee employee)
    {
        var content = GenerateSearchableContent(employee);
        var embedding = await _openAIService.GenerateEmbeddingAsync(content);

        return new EmployeeSearchDocument
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
            Content = content,
            Embedding = embedding
        };
    }

    private static string GenerateSearchableContent(Employee employee)
    {
        var skillsText = string.Join(", ", employee.Skills.Select(s => $"{s.Name} ({s.Level}, {s.YearsUsed} years)"));
        var certsText = string.Join(", ", employee.Certifications);

        return $"""
            Name: {employee.Name}
            Job Title: {employee.JobTitle}
            Department: {employee.Department}
            Years of Experience: {employee.YearsOfExperience}
            Skills: {skillsText}
            Certifications: {certsText}
            Location: {employee.Location}
            Availability: {employee.Availability}
            Bio: {employee.Bio}
            Preferred Project Types: {string.Join(", ", employee.PreferredProjectTypes)}
            """;
    }
}
