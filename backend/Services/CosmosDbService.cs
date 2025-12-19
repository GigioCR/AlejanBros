using AlejanBros.Configuration;
using AlejanBros.Models;
using AlejanBros.Domain.Repositories;
using AlejanBros.Application.Adapters;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlejanBros.Services;

public class CosmosDbService : ICosmosDbService
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly ILogger<CosmosDbService> _logger;

    public CosmosDbService(
        IEmployeeRepository employeeRepository,
        IProjectRepository projectRepository,
        ILogger<CosmosDbService> logger)
    {
        _employeeRepository = employeeRepository;
        _projectRepository = projectRepository;
        _logger = logger;
    }

    #region Employee Operations

    public async Task<Employee?> GetEmployeeAsync(string id)
    {
        var domainEmployee = await _employeeRepository.GetByIdAsync(id);
        return domainEmployee != null ? ModelAdapter.ToLegacyModel(domainEmployee) : null;
    }

    public async Task<IEnumerable<Employee>> GetAllEmployeesAsync()
    {
        var domainEmployees = await _employeeRepository.GetAllAsync();
        return domainEmployees.Select(ModelAdapter.ToLegacyModel);
    }

    public async Task<PaginatedResult<Employee>> GetEmployeesAsync(int page = 1, int pageSize = 10)
    {
        var (domainEmployees, totalCount) = await _employeeRepository.GetPagedAsync(page, pageSize);
        var employees = domainEmployees.Select(ModelAdapter.ToLegacyModel).ToList();

        return new PaginatedResult<Employee>
        {
            Items = employees,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<Employee> CreateEmployeeAsync(Employee employee)
    {
        var domainEmployee = ModelAdapter.FromLegacyModel(employee);
        var created = await _employeeRepository.CreateAsync(domainEmployee);
        return ModelAdapter.ToLegacyModel(created);
    }

    public async Task<Employee> UpdateEmployeeAsync(Employee employee)
    {
        var domainEmployee = ModelAdapter.FromLegacyModel(employee);
        var updated = await _employeeRepository.UpdateAsync(domainEmployee);
        return ModelAdapter.ToLegacyModel(updated);
    }

    public async Task DeleteEmployeeAsync(string id)
    {
        await _employeeRepository.DeleteAsync(id);
    }

    public async Task<IEnumerable<Employee>> SearchEmployeesBySkillsAsync(IEnumerable<string> skills)
    {
        var domainEmployees = await _employeeRepository.SearchBySkillsAsync(skills);
        return domainEmployees.Select(ModelAdapter.ToLegacyModel);
    }

    #endregion

    #region Project Operations

    public async Task<Project?> GetProjectAsync(string id)
    {
        var domainProject = await _projectRepository.GetByIdAsync(id);
        return domainProject != null ? ModelAdapter.ToLegacyModel(domainProject) : null;
    }

    public async Task<PaginatedResult<Project>> GetProjectsAsync(int page = 1, int pageSize = 10)
    {
        var (domainProjects, totalCount) = await _projectRepository.GetPagedAsync(page, pageSize);
        var projects = domainProjects.Select(ModelAdapter.ToLegacyModel).ToList();

        return new PaginatedResult<Project>
        {
            Items = projects,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<Project> CreateProjectAsync(Project project)
    {
        var domainProject = ModelAdapter.FromLegacyModel(project);
        var created = await _projectRepository.CreateAsync(domainProject);
        return ModelAdapter.ToLegacyModel(created);
    }

    public async Task<Project> UpdateProjectAsync(Project project)
    {
        var domainProject = ModelAdapter.FromLegacyModel(project);
        var updated = await _projectRepository.UpdateAsync(domainProject);
        return ModelAdapter.ToLegacyModel(updated);
    }

    public async Task DeleteProjectAsync(string id)
    {
        await _projectRepository.DeleteAsync(id);
    }

    #endregion
}
