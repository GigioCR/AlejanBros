using AlejanBros.Domain.Entities;

namespace AlejanBros.Domain.Repositories;

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(string id);
    Task<(IEnumerable<Project> Items, int TotalCount)> GetPagedAsync(int page, int pageSize);
    Task<Project> CreateAsync(Project project);
    Task<Project> UpdateAsync(Project project);
    Task DeleteAsync(string id);
}
