namespace AlejanBros.Domain.Entities;

public class User
{
    public string Id { get; private set; }
    public string Email { get; private set; }
    public string Name { get; private set; }
    public string PasswordHash { get; private set; }
    public string Role { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private User()
    {
        Id = string.Empty;
        Email = string.Empty;
        Name = string.Empty;
        PasswordHash = string.Empty;
        Role = string.Empty;
    }

    public User(string email, string name, string passwordHash, string role = "User")
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required", nameof(email));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required", nameof(name));
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash is required", nameof(passwordHash));

        Id = Guid.NewGuid().ToString();
        Email = email;
        Name = name;
        PasswordHash = passwordHash;
        Role = role;
        CreatedAt = DateTime.UtcNow;
    }

    public static User Reconstruct(
        string id,
        string email,
        string name,
        string passwordHash,
        string role,
        DateTime createdAt)
    {
        return new User
        {
            Id = id,
            Email = email,
            Name = name,
            PasswordHash = passwordHash,
            Role = role,
            CreatedAt = createdAt
        };
    }

    public bool VerifyPassword(string passwordHash)
    {
        return PasswordHash == passwordHash;
    }
}
