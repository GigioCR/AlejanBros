using AlejanBros.Application.Common;
using AlejanBros.Application.Common.Exceptions;
using AlejanBros.Application.DTOs;
using AlejanBros.Application.Interfaces;
using AlejanBros.Application.Mappings;
using AlejanBros.Domain.Entities;
using AlejanBros.Domain.Enums;
using AlejanBros.Domain.Repositories;
using AlejanBros.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace AlejanBros.Application.Services;

public interface IEmployeeService
{
    Task<Result<EmployeeDto>> GetByIdAsync(string id);
    Task<Result<PaginatedResult<EmployeeDto>>> GetPagedAsync(int page, int pageSize);
    Task<Result<EmployeeDto>> CreateAsync(EmployeeDto dto);
    Task<Result<EmployeeDto>> UpdateAsync(EmployeeDto dto);
    Task<Result> DeleteAsync(string id);
}

public class EmployeeService : IEmployeeService
{
    private readonly IEmployeeRepository _repository;
    private readonly IEmbeddingService _embeddingService;
    private readonly ISearchRepository _searchRepository;
    private readonly ILogger<EmployeeService> _logger;

    public EmployeeService(
        IEmployeeRepository repository,
        IEmbeddingService embeddingService,
        ISearchRepository searchRepository,
        ILogger<EmployeeService> logger)
    {
        _repository = repository;
        _embeddingService = embeddingService;
        _searchRepository = searchRepository;
        _logger = logger;
    }

    public async Task<Result<EmployeeDto>> GetByIdAsync(string id)
    {
        try
        {
            var employee = await _repository.GetByIdAsync(id);
            if (employee == null)
                return Result.Failure<EmployeeDto>($"Employee with ID {id} not found");

            return Result.Success(EmployeeMapper.ToDto(employee));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving employee {Id}", id);
            return Result.Failure<EmployeeDto>("An error occurred while retrieving the employee");
        }
    }

    public async Task<Result<PaginatedResult<EmployeeDto>>> GetPagedAsync(int page, int pageSize)
    {
        try
        {
            var (items, totalCount) = await _repository.GetPagedAsync(page, pageSize);
            var dtos = items.Select(EmployeeMapper.ToDto);

            var result = new PaginatedResult<EmployeeDto>
            {
                Items = dtos,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };

            return Result.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged employees");
            return Result.Failure<PaginatedResult<EmployeeDto>>("An error occurred while retrieving employees");
        }
    }

    public async Task<Result<EmployeeDto>> CreateAsync(EmployeeDto dto)
    {
        try
        {
            var skills = dto.Skills.Select(s => new Skill(s.Name, (SkillLevel)s.Level, s.YearsUsed)).ToList();
            
            var employee = new Employee(
                dto.Name,
                dto.Email,
                dto.Department,
                dto.JobTitle,
                dto.YearsOfExperience,
                (AvailabilityStatus)dto.Availability);

            foreach (var skill in skills)
            {
                employee.AddSkill(skill);
            }

            // Generate embedding
            var embeddingText = $"{employee.Name} {employee.JobTitle} {employee.Department} {string.Join(" ", employee.Skills.Select(s => s.Name))} {employee.Bio}";
            var embedding = await _embeddingService.GenerateEmbeddingAsync(embeddingText);
            employee.SetEmbedding(embedding);

            var created = await _repository.CreateAsync(employee);
            
            // Index in search
            await _searchRepository.IndexEmployeeAsync(created.Id);

            return Result.Success(EmployeeMapper.ToDto(created));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating employee");
            return Result.Failure<EmployeeDto>("An error occurred while creating the employee");
        }
    }

    public async Task<Result<EmployeeDto>> UpdateAsync(EmployeeDto dto)
    {
        try
        {
            var existing = await _repository.GetByIdAsync(dto.Id);
            if (existing == null)
                return Result.Failure<EmployeeDto>($"Employee with ID {dto.Id} not found");

            existing.Update(
                dto.Name,
                dto.Email,
                dto.Department,
                dto.JobTitle,
                dto.YearsOfExperience,
                (AvailabilityStatus)dto.Availability,
                dto.Location,
                dto.Bio);

            var updated = await _repository.UpdateAsync(existing);
            
            // Re-index in search
            await _searchRepository.IndexEmployeeAsync(updated.Id);

            return Result.Success(EmployeeMapper.ToDto(updated));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating employee {Id}", dto.Id);
            return Result.Failure<EmployeeDto>("An error occurred while updating the employee");
        }
    }

    public async Task<Result> DeleteAsync(string id)
    {
        try
        {
            var existing = await _repository.GetByIdAsync(id);
            if (existing == null)
                return Result.Failure($"Employee with ID {id} not found");

            await _repository.DeleteAsync(id);
            await _searchRepository.DeleteEmployeeFromIndexAsync(id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting employee {Id}", id);
            return Result.Failure("An error occurred while deleting the employee");
        }
    }
}
