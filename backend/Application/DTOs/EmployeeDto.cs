using System.Text.Json.Serialization;

namespace AlejanBros.Application.DTOs;

public class EmployeeDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("department")]
    public string Department { get; set; } = string.Empty;

    [JsonPropertyName("jobTitle")]
    public string JobTitle { get; set; } = string.Empty;

    [JsonPropertyName("skills")]
    public List<SkillDto> Skills { get; set; } = new();

    [JsonPropertyName("yearsOfExperience")]
    public int YearsOfExperience { get; set; }

    [JsonPropertyName("certifications")]
    public List<string> Certifications { get; set; } = new();

    [JsonPropertyName("availability")]
    public int Availability { get; set; }

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
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}

public class SkillDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("yearsUsed")]
    public int YearsUsed { get; set; }
}
