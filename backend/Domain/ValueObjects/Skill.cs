using AlejanBros.Domain.Enums;

namespace AlejanBros.Domain.ValueObjects;

public class Skill
{
    public string Name { get; private set; }
    public SkillLevel Level { get; private set; }
    public int YearsUsed { get; private set; }

    private Skill() { Name = string.Empty; } // For serialization

    public Skill(string name, SkillLevel level, int yearsUsed)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Skill name cannot be empty", nameof(name));
        
        if (yearsUsed < 0)
            throw new ArgumentException("Years used cannot be negative", nameof(yearsUsed));

        Name = name;
        Level = level;
        YearsUsed = yearsUsed;
    }

    public bool Matches(string skillName, SkillLevel? minimumLevel = null)
    {
        var nameMatches = Name.Equals(skillName, StringComparison.OrdinalIgnoreCase);
        var levelMatches = !minimumLevel.HasValue || Level >= minimumLevel.Value;
        return nameMatches && levelMatches;
    }
}
