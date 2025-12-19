using AlejanBros.Application.DTOs;
using AlejanBros.Domain.Entities;
using AlejanBros.Domain.Enums;
using AlejanBros.Domain.ValueObjects;

namespace AlejanBros.Application.Mappings;

public static class EmployeeMapper
{
    public static EmployeeDto ToDto(Employee employee)
    {
        return new EmployeeDto
        {
            Id = employee.Id,
            Name = employee.Name,
            Email = employee.Email,
            Department = employee.Department,
            JobTitle = employee.JobTitle,
            Skills = employee.Skills.Select(s => new SkillDto
            {
                Name = s.Name,
                Level = (int)s.Level,
                YearsUsed = s.YearsUsed
            }).ToList(),
            YearsOfExperience = employee.YearsOfExperience,
            Certifications = employee.Certifications,
            Availability = (int)employee.Availability,
            CurrentProjects = employee.CurrentProjects,
            PreferredProjectTypes = employee.PreferredProjectTypes,
            Location = employee.Location,
            Bio = employee.Bio,
            Embedding = employee.Embedding,
            CreatedAt = employee.CreatedAt,
            UpdatedAt = employee.UpdatedAt
        };
    }

    public static Employee ToDomain(EmployeeDto dto)
    {
        var skills = dto.Skills.Select(s => new Skill(s.Name, (SkillLevel)s.Level, s.YearsUsed)).ToList();
        
        return Employee.Reconstruct(
            dto.Id,
            dto.Name,
            dto.Email,
            dto.Department,
            dto.JobTitle,
            skills,
            dto.YearsOfExperience,
            dto.Certifications,
            (AvailabilityStatus)dto.Availability,
            dto.CurrentProjects,
            dto.PreferredProjectTypes,
            dto.Location,
            dto.Bio,
            dto.Embedding,
            dto.CreatedAt,
            dto.UpdatedAt
        );
    }
}
