using AlejanBros.Domain.Entities;

namespace AlejanBros.Domain.Repositories;

public interface IEmployeeRepository
{
    Task<Employee?> GetByIdAsync(string id);
    Task<IEnumerable<Employee>> GetAllAsync();
    Task<(IEnumerable<Employee> Items, int TotalCount)> GetPagedAsync(int page, int pageSize);
    Task<Employee> CreateAsync(Employee employee);
    Task<Employee> UpdateAsync(Employee employee);
    Task DeleteAsync(string id);
    Task<IEnumerable<Employee>> SearchBySkillsAsync(IEnumerable<string> skills);
}
