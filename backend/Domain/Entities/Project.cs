using AlejanBros.Domain.Enums;
using AlejanBros.Domain.ValueObjects;

namespace AlejanBros.Domain.Entities;

public class Project
{
    public string Id { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public List<RequiredSkill> RequiredSkills { get; private set; }
    public List<string> TechStack { get; private set; }
    public int TeamSize { get; private set; }
    public string Duration { get; private set; }
    public DateTime? StartDate { get; private set; }
    public ProjectPriority Priority { get; private set; }
    public string ProjectType { get; private set; }
    public string Client { get; private set; }
    public ProjectStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Private constructor for ORM/serialization
    private Project()
    {
        Id = string.Empty;
        Name = string.Empty;
        Description = string.Empty;
        RequiredSkills = new List<RequiredSkill>();
        TechStack = new List<string>();
        Duration = string.Empty;
        ProjectType = string.Empty;
        Client = string.Empty;
    }

    public Project(
        string name,
        string description,
        int teamSize,
        ProjectStatus status = ProjectStatus.Planning,
        ProjectPriority priority = ProjectPriority.Medium)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Project name is required", nameof(name));
        if (teamSize <= 0)
            throw new ArgumentException("Team size must be positive", nameof(teamSize));

        Id = Guid.NewGuid().ToString();
        Name = name;
        Description = description;
        TeamSize = teamSize;
        Status = status;
        Priority = priority;
        RequiredSkills = new List<RequiredSkill>();
        TechStack = new List<string>();
        Duration = string.Empty;
        ProjectType = string.Empty;
        Client = string.Empty;
        CreatedAt = DateTime.UtcNow;
    }

    public static Project Reconstruct(
        string id,
        string name,
        string description,
        List<RequiredSkill> requiredSkills,
        List<string> techStack,
        int teamSize,
        string duration,
        DateTime? startDate,
        ProjectPriority priority,
        string projectType,
        string client,
        ProjectStatus status,
        DateTime createdAt)
    {
        return new Project
        {
            Id = id,
            Name = name,
            Description = description,
            RequiredSkills = requiredSkills,
            TechStack = techStack,
            TeamSize = teamSize,
            Duration = duration,
            StartDate = startDate,
            Priority = priority,
            ProjectType = projectType,
            Client = client,
            Status = status,
            CreatedAt = createdAt
        };
    }

    public void AddRequiredSkill(RequiredSkill skill)
    {
        if (!RequiredSkills.Any(s => s.Name.Equals(skill.Name, StringComparison.OrdinalIgnoreCase)))
        {
            RequiredSkills.Add(skill);
        }
    }

    public void UpdateStatus(ProjectStatus newStatus)
    {
        Status = newStatus;
    }

    public void Update(
        string? name = null,
        string? description = null,
        int? teamSize = null,
        ProjectPriority? priority = null,
        ProjectStatus? status = null)
    {
        if (!string.IsNullOrWhiteSpace(name)) Name = name;
        if (!string.IsNullOrWhiteSpace(description)) Description = description;
        if (teamSize.HasValue && teamSize.Value > 0) TeamSize = teamSize.Value;
        if (priority.HasValue) Priority = priority.Value;
        if (status.HasValue) Status = status.Value;
    }
}
