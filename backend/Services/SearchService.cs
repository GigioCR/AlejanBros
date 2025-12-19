using AlejanBros.Configuration;
using AlejanBros.Models;
using AlejanBros.Domain.Repositories;
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
    private readonly ISearchRepository _searchRepository;
    private readonly ILogger<SearchService> _logger;

    public SearchService(
        ISearchRepository searchRepository,
        ILogger<SearchService> logger)
    {
        _searchRepository = searchRepository;
        _logger = logger;
    }

    public async Task InitializeIndexAsync()
    {
        await _searchRepository.InitializeIndexAsync();
    }

    public async Task IndexEmployeeAsync(Employee employee)
    {
        await _searchRepository.IndexEmployeeAsync(employee.Id);
    }

    public async Task IndexEmployeesAsync(IEnumerable<Employee> employees)
    {
        var employeeIds = employees.Select(e => e.Id);
        await _searchRepository.IndexEmployeesAsync(employeeIds);
    }

    public async Task DeleteEmployeeFromIndexAsync(string employeeId)
    {
        await _searchRepository.DeleteEmployeeFromIndexAsync(employeeId);
    }

    public async Task ClearAndRebuildIndexAsync(IEnumerable<Employee> employees)
    {
        await _searchRepository.RebuildIndexAsync();
    }

    public async Task<IEnumerable<EmployeeSearchDocument>> SearchAsync(string query, int top = 10)
    {
        var results = await _searchRepository.SearchAsync(query, top);
        return results.Select(MapToEmployeeSearchDocument);
    }

    public async Task<IEnumerable<EmployeeSearchDocument>> VectorSearchAsync(float[] embedding, int top = 10)
    {
        var results = await _searchRepository.VectorSearchAsync(embedding, top);
        return results.Select(MapToEmployeeSearchDocument);
    }

    public async Task<IEnumerable<EmployeeSearchDocument>> HybridSearchAsync(string query, float[] embedding, int top = 10)
    {
        var employeeIds = await _searchRepository.HybridSearchAsync(query, embedding, top);
        var results = await _searchRepository.SearchAsync(query, top);
        return results.Where(r => employeeIds.Contains(r.Id)).Select(MapToEmployeeSearchDocument);
    }

    private EmployeeSearchDocument MapToEmployeeSearchDocument(Domain.Repositories.SearchDocument doc)
    {
        return new EmployeeSearchDocument
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
