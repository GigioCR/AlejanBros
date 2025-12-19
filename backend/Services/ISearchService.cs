using AlejanBros.Models;

namespace AlejanBros.Services;

public interface ISearchService
{
    Task InitializeIndexAsync();
    Task IndexEmployeeAsync(Employee employee);
    Task IndexEmployeesAsync(IEnumerable<Employee> employees);
    Task DeleteEmployeeFromIndexAsync(string employeeId);
    Task ClearAndRebuildIndexAsync(IEnumerable<Employee> employees);
    Task<IEnumerable<EmployeeSearchDocument>> SearchAsync(string query, int top = 10);
    Task<IEnumerable<EmployeeSearchDocument>> VectorSearchAsync(float[] embedding, int top = 10);
    Task<IEnumerable<EmployeeSearchDocument>> HybridSearchAsync(string query, float[] embedding, int top = 10);
}
