using System.Text.Json.Serialization;

namespace AlejanBros.Models;

public class Project
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("requiredSkills")]
    public List<RequiredSkill> RequiredSkills { get; set; } = new();

    [JsonPropertyName("techStack")]
    public List<string> TechStack { get; set; } = new();

    [JsonPropertyName("teamSize")]
    public int TeamSize { get; set; }

    [JsonPropertyName("duration")]
    public string Duration { get; set; } = string.Empty;

    [JsonPropertyName("startDate")]
    public DateTime? StartDate { get; set; }

    [JsonPropertyName("priority")]
    public ProjectPriority Priority { get; set; } = ProjectPriority.Medium;

    [JsonPropertyName("projectType")]
    public string ProjectType { get; set; } = string.Empty;

    [JsonPropertyName("client")]
    public string Client { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public ProjectStatus Status { get; set; } = ProjectStatus.Planning;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class RequiredSkill
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("minimumLevel")]
    public SkillLevel MinimumLevel { get; set; } = SkillLevel.Intermediate;

    [JsonPropertyName("required")]
    public bool Required { get; set; } = true;
}

public enum ProjectPriority
{
    Low,
    Medium,
    High,
    Critical
}

public enum ProjectStatus
{
    Planning,
    Active,
    OnHold,
    Completed
}
