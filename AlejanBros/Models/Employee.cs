using System.Text.Json.Serialization;

namespace AlejanBros.Models;

public class Employee
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("department")]
    public string Department { get; set; } = string.Empty;

    [JsonPropertyName("jobTitle")]
    public string JobTitle { get; set; } = string.Empty;

    [JsonPropertyName("skills")]
    public List<Skill> Skills { get; set; } = new();

    [JsonPropertyName("yearsOfExperience")]
    public int YearsOfExperience { get; set; }

    [JsonPropertyName("certifications")]
    public List<string> Certifications { get; set; } = new();

    [JsonPropertyName("availability")]
    public AvailabilityStatus Availability { get; set; } = AvailabilityStatus.Available;

    [JsonPropertyName("currentProjects")]
    public List<string> CurrentProjects { get; set; } = new();

    [JsonPropertyName("preferredProjectTypes")]
    public List<string> PreferredProjectTypes { get; set; } = new();

    [JsonPropertyName("location")]
    public string Location { get; set; } = string.Empty;

    [JsonPropertyName("bio")]
    public string Bio { get; set; } = string.Empty;

    [JsonPropertyName("embedding")]
    public float[]? Embedding { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class Skill
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("level")]
    public SkillLevel Level { get; set; } = SkillLevel.Intermediate;

    [JsonPropertyName("yearsUsed")]
    public int YearsUsed { get; set; }
}

public enum SkillLevel
{
    Beginner = 1,
    Intermediate = 2,
    Advanced = 3,
    Expert = 4
}

public enum AvailabilityStatus
{
    Available,
    PartiallyAvailable,
    Unavailable
}
