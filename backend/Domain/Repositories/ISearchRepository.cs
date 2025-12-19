namespace AlejanBros.Domain.Repositories;

public interface ISearchRepository
{
    Task InitializeIndexAsync();
    Task<IEnumerable<string>> HybridSearchAsync(string query, float[] embedding, int top = 10);
    Task<IEnumerable<SearchDocument>> SearchAsync(string query, int top = 10);
    Task<IEnumerable<SearchDocument>> VectorSearchAsync(float[] embedding, int top = 10);
    Task IndexEmployeeAsync(string employeeId);
    Task IndexEmployeesAsync(IEnumerable<string> employeeIds);
    Task DeleteEmployeeFromIndexAsync(string employeeId);
    Task RebuildIndexAsync();
}

public class SearchDocument
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string[] Skills { get; set; } = Array.Empty<string>();
    public int YearsOfExperience { get; set; }
    public string[] Certifications { get; set; } = Array.Empty<string>();
    public string Availability { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    public float[]? Embedding { get; set; }
}
