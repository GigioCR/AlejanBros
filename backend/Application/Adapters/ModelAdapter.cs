using AlejanBros.Domain.Entities;
using AlejanBros.Domain.Enums;
using AlejanBros.Domain.ValueObjects;

namespace AlejanBros.Application.Adapters;

public static class ModelAdapter
{
    public static Models.Employee ToLegacyModel(Employee domain)
    {
        return new Models.Employee
        {
            Id = domain.Id,
            Name = domain.Name,
            Email = domain.Email,
            Department = domain.Department,
            JobTitle = domain.JobTitle,
            Skills = domain.Skills.Select(s => new Models.Skill
            {
                Name = s.Name,
                Level = (Models.SkillLevel)s.Level,
                YearsUsed = s.YearsUsed
            }).ToList(),
            YearsOfExperience = domain.YearsOfExperience,
            Certifications = domain.Certifications.ToList(),
            Availability = (Models.AvailabilityStatus)domain.Availability,
            CurrentProjects = domain.CurrentProjects.ToList(),
            PreferredProjectTypes = domain.PreferredProjectTypes.ToList(),
            Location = domain.Location,
            Bio = domain.Bio,
            Embedding = domain.Embedding,
            CreatedAt = domain.CreatedAt,
            UpdatedAt = domain.UpdatedAt
        };
    }

    public static Employee FromLegacyModel(Models.Employee model)
    {
        var skills = model.Skills.Select(s => 
            new Skill(s.Name, (SkillLevel)s.Level, s.YearsUsed)).ToList();

        return Employee.Reconstruct(
            model.Id,
            model.Name,
            model.Email,
            model.Department,
            model.JobTitle,
            skills,
            model.YearsOfExperience,
            model.Certifications,
            (AvailabilityStatus)model.Availability,
            model.CurrentProjects,
            model.PreferredProjectTypes,
            model.Location,
            model.Bio,
            model.Embedding,
            model.CreatedAt,
            model.UpdatedAt
        );
    }

    public static Models.Project ToLegacyModel(Project domain)
    {
        return new Models.Project
        {
            Id = domain.Id,
            Name = domain.Name,
            Description = domain.Description,
            RequiredSkills = domain.RequiredSkills.Select(s => new Models.RequiredSkill
            {
                Name = s.Name,
                MinimumLevel = (Models.SkillLevel)s.MinimumLevel,
                Required = s.Required
            }).ToList(),
            TechStack = domain.TechStack.ToList(),
            TeamSize = domain.TeamSize,
            Duration = domain.Duration,
            StartDate = domain.StartDate,
            Priority = (Models.ProjectPriority)domain.Priority,
            ProjectType = domain.ProjectType,
            Client = domain.Client,
            Status = (Models.ProjectStatus)domain.Status,
            CreatedAt = domain.CreatedAt
        };
    }

    public static Project FromLegacyModel(Models.Project model)
    {
        var requiredSkills = model.RequiredSkills.Select(s =>
            new RequiredSkill(s.Name, (SkillLevel)s.MinimumLevel, s.Required)).ToList();

        return Project.Reconstruct(
            model.Id,
            model.Name,
            model.Description,
            requiredSkills,
            model.TechStack,
            model.TeamSize,
            model.Duration,
            model.StartDate,
            (ProjectPriority)model.Priority,
            model.ProjectType,
            model.Client,
            (ProjectStatus)model.Status,
            model.CreatedAt
        );
    }
}
