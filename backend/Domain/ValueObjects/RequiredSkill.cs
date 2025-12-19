using AlejanBros.Domain.Enums;

namespace AlejanBros.Domain.ValueObjects;

public class RequiredSkill
{
    public string Name { get; private set; }
    public SkillLevel MinimumLevel { get; private set; }
    public bool Required { get; private set; }

    private RequiredSkill() { Name = string.Empty; } // For serialization

    public RequiredSkill(string name, SkillLevel minimumLevel, bool required = true)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Skill name cannot be empty", nameof(name));

        Name = name;
        MinimumLevel = minimumLevel;
        Required = required;
    }
}
