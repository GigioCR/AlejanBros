using AlejanBros.Application.DTOs;
using AlejanBros.Domain.Entities;
using AlejanBros.Domain.Enums;
using AlejanBros.Domain.ValueObjects;

namespace AlejanBros.Application.Mappings;

public static class ProjectMapper
{
    public static ProjectDto ToDto(Project project)
    {
        return new ProjectDto
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            RequiredSkills = project.RequiredSkills.Select(s => new RequiredSkillDto
            {
                Name = s.Name,
                MinimumLevel = (int)s.MinimumLevel,
                Required = s.Required
            }).ToList(),
            TechStack = project.TechStack,
            TeamSize = project.TeamSize,
            Duration = project.Duration,
            StartDate = project.StartDate,
            Priority = (int)project.Priority,
            ProjectType = project.ProjectType,
            Client = project.Client,
            Status = (int)project.Status,
            CreatedAt = project.CreatedAt
        };
    }

    public static Project ToDomain(ProjectDto dto)
    {
        var requiredSkills = dto.RequiredSkills.Select(s => 
            new RequiredSkill(s.Name, (SkillLevel)s.MinimumLevel, s.Required)).ToList();
        
        return Project.Reconstruct(
            dto.Id,
            dto.Name,
            dto.Description,
            requiredSkills,
            dto.TechStack,
            dto.TeamSize,
            dto.Duration,
            dto.StartDate,
            (ProjectPriority)dto.Priority,
            dto.ProjectType,
            dto.Client,
            (ProjectStatus)dto.Status,
            dto.CreatedAt
        );
    }
}
