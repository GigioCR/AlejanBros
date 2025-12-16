using AlejanBros.Models;

namespace AlejanBros.Services;

public interface ICosmosDbService
{
    // Employee operations
    Task<Employee?> GetEmployeeAsync(string id);
    Task<IEnumerable<Employee>> GetAllEmployeesAsync();
    Task<Employee> CreateEmployeeAsync(Employee employee);
    Task<Employee> UpdateEmployeeAsync(Employee employee);
    Task DeleteEmployeeAsync(string id);
    Task<IEnumerable<Employee>> SearchEmployeesBySkillsAsync(IEnumerable<string> skills);

    // Project operations
    Task<Project?> GetProjectAsync(string id);
    Task<IEnumerable<Project>> GetAllProjectsAsync();
    Task<Project> CreateProjectAsync(Project project);
    Task<Project> UpdateProjectAsync(Project project);
    Task DeleteProjectAsync(string id);
}
