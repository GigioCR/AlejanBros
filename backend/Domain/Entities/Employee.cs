using AlejanBros.Domain.Enums;
using AlejanBros.Domain.ValueObjects;

namespace AlejanBros.Domain.Entities;

public class Employee
{
    public string Id { get; private set; }
    public string Name { get; private set; }
    public string Email { get; private set; }
    public string Department { get; private set; }
    public string JobTitle { get; private set; }
    public List<Skill> Skills { get; private set; }
    public int YearsOfExperience { get; private set; }
    public List<string> Certifications { get; private set; }
    public AvailabilityStatus Availability { get; private set; }
    public List<string> CurrentProjects { get; private set; }
    public List<string> PreferredProjectTypes { get; private set; }
    public string Location { get; private set; }
    public string Bio { get; private set; }
    public float[]? Embedding { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // Private constructor for ORM/serialization
    private Employee()
    {
        Id = string.Empty;
        Name = string.Empty;
        Email = string.Empty;
        Department = string.Empty;
        JobTitle = string.Empty;
        Skills = new List<Skill>();
        Certifications = new List<string>();
        CurrentProjects = new List<string>();
        PreferredProjectTypes = new List<string>();
        Location = string.Empty;
        Bio = string.Empty;
    }

    public Employee(
        string name,
        string email,
        string department,
        string jobTitle,
        int yearsOfExperience,
        AvailabilityStatus availability = AvailabilityStatus.Available)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required", nameof(name));
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required", nameof(email));
        if (yearsOfExperience < 0)
            throw new ArgumentException("Years of experience cannot be negative", nameof(yearsOfExperience));

        Id = Guid.NewGuid().ToString();
        Name = name;
        Email = email;
        Department = department;
        JobTitle = jobTitle;
        YearsOfExperience = yearsOfExperience;
        Availability = availability;
        Skills = new List<Skill>();
        Certifications = new List<string>();
        CurrentProjects = new List<string>();
        PreferredProjectTypes = new List<string>();
        Location = string.Empty;
        Bio = string.Empty;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    // Factory method for reconstruction from persistence
    public static Employee Reconstruct(
        string id,
        string name,
        string email,
        string department,
        string jobTitle,
        List<Skill> skills,
        int yearsOfExperience,
        List<string> certifications,
        AvailabilityStatus availability,
        List<string> currentProjects,
        List<string> preferredProjectTypes,
        string location,
        string bio,
        float[]? embedding,
        DateTime createdAt,
        DateTime updatedAt)
    {
        return new Employee
        {
            Id = id,
            Name = name,
            Email = email,
            Department = department,
            JobTitle = jobTitle,
            Skills = skills,
            YearsOfExperience = yearsOfExperience,
            Certifications = certifications,
            Availability = availability,
            CurrentProjects = currentProjects,
            PreferredProjectTypes = preferredProjectTypes,
            Location = location,
            Bio = bio,
            Embedding = embedding,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }

    public void UpdateAvailability(AvailabilityStatus newStatus)
    {
        Availability = newStatus;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddSkill(Skill skill)
    {
        if (!Skills.Any(s => s.Name.Equals(skill.Name, StringComparison.OrdinalIgnoreCase)))
        {
            Skills.Add(skill);
            UpdatedAt = DateTime.UtcNow;
        }
    }

    public void SetEmbedding(float[] embedding)
    {
        Embedding = embedding;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool HasSkill(string skillName, SkillLevel? minimumLevel = null)
    {
        return Skills.Any(s => s.Matches(skillName, minimumLevel));
    }

    public void Update(
        string? name = null,
        string? email = null,
        string? department = null,
        string? jobTitle = null,
        int? yearsOfExperience = null,
        AvailabilityStatus? availability = null,
        string? location = null,
        string? bio = null)
    {
        if (!string.IsNullOrWhiteSpace(name)) Name = name;
        if (!string.IsNullOrWhiteSpace(email)) Email = email;
        if (!string.IsNullOrWhiteSpace(department)) Department = department;
        if (!string.IsNullOrWhiteSpace(jobTitle)) JobTitle = jobTitle;
        if (yearsOfExperience.HasValue && yearsOfExperience.Value >= 0) YearsOfExperience = yearsOfExperience.Value;
        if (availability.HasValue) Availability = availability.Value;
        if (!string.IsNullOrWhiteSpace(location)) Location = location;
        if (!string.IsNullOrWhiteSpace(bio)) Bio = bio;
        
        UpdatedAt = DateTime.UtcNow;
    }
}
