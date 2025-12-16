using AlejanBros.Models;
using AlejanBros.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AlejanBros.Functions;

public class AdminFunctions
{
    private readonly ICosmosDbService _cosmosDbService;
    private readonly ISearchService _searchService;
    private readonly ILogger<AdminFunctions> _logger;

    public AdminFunctions(
        ICosmosDbService cosmosDbService,
        ISearchService searchService,
        ILogger<AdminFunctions> logger)
    {
        _cosmosDbService = cosmosDbService;
        _searchService = searchService;
        _logger = logger;
    }

    [Function("InitializeSearchIndex")]
    public async Task<IActionResult> InitializeSearchIndex(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "admin/initialize-index")] HttpRequest req)
    {
        _logger.LogInformation("Initializing search index");

        try
        {
            await _searchService.InitializeIndexAsync();
            return new OkObjectResult(new { message = "Search index initialized successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing search index");
            return new StatusCodeResult(500);
        }
    }

    [Function("ReindexAllEmployees")]
    public async Task<IActionResult> ReindexAllEmployees(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "admin/reindex")] HttpRequest req)
    {
        _logger.LogInformation("Reindexing all employees");

        try
        {
            var employees = await _cosmosDbService.GetAllEmployeesAsync();
            await _searchService.IndexEmployeesAsync(employees);

            return new OkObjectResult(new
            {
                message = "Reindexing completed",
                employeesIndexed = employees.Count()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reindexing employees");
            return new StatusCodeResult(500);
        }
    }

    [Function("SeedSampleData")]
    public async Task<IActionResult> SeedSampleData(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "admin/seed")] HttpRequest req)
    {
        _logger.LogInformation("Seeding sample data");

        try
        {
            var employees = GetSampleEmployees();
            var projects = GetSampleProjects();

            foreach (var employee in employees)
            {
                await _cosmosDbService.CreateEmployeeAsync(employee);
            }

            foreach (var project in projects)
            {
                await _cosmosDbService.CreateProjectAsync(project);
            }

            // Index employees in search
            var allEmployees = await _cosmosDbService.GetAllEmployeesAsync();
            await _searchService.IndexEmployeesAsync(allEmployees);

            return new OkObjectResult(new
            {
                message = "Sample data seeded successfully",
                employeesCreated = employees.Count,
                projectsCreated = projects.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding sample data");
            return new StatusCodeResult(500);
        }
    }

    [Function("HealthCheck")]
    public IActionResult HealthCheck(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequest req)
    {
        return new OkObjectResult(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            version = "1.0.0"
        });
    }

    private static List<Employee> GetSampleEmployees()
    {
        return new List<Employee>
        {
            new Employee
            {
                Name = "María García",
                Email = "maria.garcia@company.com",
                Department = "Engineering",
                JobTitle = "Senior Full Stack Developer",
                YearsOfExperience = 8,
                Skills = new List<Skill>
                {
                    new Skill { Name = "C#", Level = SkillLevel.Expert, YearsUsed = 6 },
                    new Skill { Name = "Azure", Level = SkillLevel.Advanced, YearsUsed = 4 },
                    new Skill { Name = "React", Level = SkillLevel.Advanced, YearsUsed = 3 },
                    new Skill { Name = "SQL Server", Level = SkillLevel.Expert, YearsUsed = 7 },
                    new Skill { Name = "Docker", Level = SkillLevel.Intermediate, YearsUsed = 2 }
                },
                Certifications = new List<string> { "Azure Developer Associate", "AWS Solutions Architect" },
                Availability = AvailabilityStatus.Available,
                Location = "Mexico City",
                Bio = "Passionate full-stack developer with extensive experience in cloud-native applications and microservices architecture.",
                PreferredProjectTypes = new List<string> { "Cloud Migration", "Web Applications", "API Development" }
            },
            new Employee
            {
                Name = "Carlos Rodríguez",
                Email = "carlos.rodriguez@company.com",
                Department = "Data Science",
                JobTitle = "Machine Learning Engineer",
                YearsOfExperience = 5,
                Skills = new List<Skill>
                {
                    new Skill { Name = "Python", Level = SkillLevel.Expert, YearsUsed = 5 },
                    new Skill { Name = "TensorFlow", Level = SkillLevel.Advanced, YearsUsed = 3 },
                    new Skill { Name = "Azure ML", Level = SkillLevel.Advanced, YearsUsed = 2 },
                    new Skill { Name = "SQL", Level = SkillLevel.Intermediate, YearsUsed = 4 },
                    new Skill { Name = "Spark", Level = SkillLevel.Intermediate, YearsUsed = 2 }
                },
                Certifications = new List<string> { "Azure AI Engineer Associate", "Google ML Engineer" },
                Availability = AvailabilityStatus.Available,
                Location = "Guadalajara",
                Bio = "ML engineer focused on building production-ready AI solutions. Experience with NLP and computer vision projects.",
                PreferredProjectTypes = new List<string> { "AI/ML Projects", "Data Analytics", "Automation" }
            },
            new Employee
            {
                Name = "Ana Martínez",
                Email = "ana.martinez@company.com",
                Department = "Engineering",
                JobTitle = "DevOps Engineer",
                YearsOfExperience = 6,
                Skills = new List<Skill>
                {
                    new Skill { Name = "Kubernetes", Level = SkillLevel.Expert, YearsUsed = 4 },
                    new Skill { Name = "Azure DevOps", Level = SkillLevel.Expert, YearsUsed = 5 },
                    new Skill { Name = "Terraform", Level = SkillLevel.Advanced, YearsUsed = 3 },
                    new Skill { Name = "Python", Level = SkillLevel.Intermediate, YearsUsed = 4 },
                    new Skill { Name = "Docker", Level = SkillLevel.Expert, YearsUsed = 5 }
                },
                Certifications = new List<string> { "Azure DevOps Engineer Expert", "CKA" },
                Availability = AvailabilityStatus.PartiallyAvailable,
                Location = "Monterrey",
                Bio = "DevOps specialist with strong focus on CI/CD pipelines and infrastructure as code.",
                PreferredProjectTypes = new List<string> { "Infrastructure", "Cloud Migration", "DevOps Transformation" }
            },
            new Employee
            {
                Name = "Roberto Sánchez",
                Email = "roberto.sanchez@company.com",
                Department = "Engineering",
                JobTitle = "Backend Developer",
                YearsOfExperience = 4,
                Skills = new List<Skill>
                {
                    new Skill { Name = "Java", Level = SkillLevel.Advanced, YearsUsed = 4 },
                    new Skill { Name = "Spring Boot", Level = SkillLevel.Advanced, YearsUsed = 3 },
                    new Skill { Name = "Azure", Level = SkillLevel.Intermediate, YearsUsed = 2 },
                    new Skill { Name = "PostgreSQL", Level = SkillLevel.Advanced, YearsUsed = 3 },
                    new Skill { Name = "Kafka", Level = SkillLevel.Intermediate, YearsUsed = 1 }
                },
                Certifications = new List<string> { "Azure Fundamentals" },
                Availability = AvailabilityStatus.Available,
                Location = "Mexico City",
                Bio = "Backend developer specializing in microservices and event-driven architectures.",
                PreferredProjectTypes = new List<string> { "API Development", "Microservices", "Integration Projects" }
            },
            new Employee
            {
                Name = "Laura Hernández",
                Email = "laura.hernandez@company.com",
                Department = "Engineering",
                JobTitle = "Frontend Developer",
                YearsOfExperience = 3,
                Skills = new List<Skill>
                {
                    new Skill { Name = "React", Level = SkillLevel.Advanced, YearsUsed = 3 },
                    new Skill { Name = "TypeScript", Level = SkillLevel.Advanced, YearsUsed = 2 },
                    new Skill { Name = "Next.js", Level = SkillLevel.Intermediate, YearsUsed = 1 },
                    new Skill { Name = "CSS/Tailwind", Level = SkillLevel.Expert, YearsUsed = 3 },
                    new Skill { Name = "Node.js", Level = SkillLevel.Intermediate, YearsUsed = 2 }
                },
                Certifications = new List<string>(),
                Availability = AvailabilityStatus.Available,
                Location = "Remote",
                Bio = "Creative frontend developer with an eye for design and user experience.",
                PreferredProjectTypes = new List<string> { "Web Applications", "Mobile Apps", "UI/UX Projects" }
            },
            new Employee
            {
                Name = "Diego López",
                Email = "diego.lopez@company.com",
                Department = "Data Science",
                JobTitle = "Data Engineer",
                YearsOfExperience = 7,
                Skills = new List<Skill>
                {
                    new Skill { Name = "Python", Level = SkillLevel.Expert, YearsUsed = 6 },
                    new Skill { Name = "Azure Data Factory", Level = SkillLevel.Expert, YearsUsed = 4 },
                    new Skill { Name = "Databricks", Level = SkillLevel.Advanced, YearsUsed = 3 },
                    new Skill { Name = "SQL", Level = SkillLevel.Expert, YearsUsed = 7 },
                    new Skill { Name = "Power BI", Level = SkillLevel.Advanced, YearsUsed = 3 }
                },
                Certifications = new List<string> { "Azure Data Engineer Associate", "Databricks Certified" },
                Availability = AvailabilityStatus.PartiallyAvailable,
                Location = "Guadalajara",
                Bio = "Data engineer with expertise in building scalable data pipelines and analytics solutions.",
                PreferredProjectTypes = new List<string> { "Data Analytics", "ETL/ELT Projects", "BI Solutions" }
            },
            new Employee
            {
                Name = "Patricia Ruiz",
                Email = "patricia.ruiz@company.com",
                Department = "Engineering",
                JobTitle = "Solutions Architect",
                YearsOfExperience = 12,
                Skills = new List<Skill>
                {
                    new Skill { Name = "Azure", Level = SkillLevel.Expert, YearsUsed = 8 },
                    new Skill { Name = "Architecture Design", Level = SkillLevel.Expert, YearsUsed = 10 },
                    new Skill { Name = "C#", Level = SkillLevel.Advanced, YearsUsed = 10 },
                    new Skill { Name = "Microservices", Level = SkillLevel.Expert, YearsUsed = 6 },
                    new Skill { Name = "Security", Level = SkillLevel.Advanced, YearsUsed = 5 }
                },
                Certifications = new List<string> { "Azure Solutions Architect Expert", "TOGAF Certified" },
                Availability = AvailabilityStatus.PartiallyAvailable,
                Location = "Mexico City",
                Bio = "Experienced architect with a track record of designing enterprise-scale cloud solutions.",
                PreferredProjectTypes = new List<string> { "Enterprise Architecture", "Cloud Migration", "Digital Transformation" }
            },
            new Employee
            {
                Name = "Fernando Torres",
                Email = "fernando.torres@company.com",
                Department = "Engineering",
                JobTitle = "Mobile Developer",
                YearsOfExperience = 5,
                Skills = new List<Skill>
                {
                    new Skill { Name = "React Native", Level = SkillLevel.Expert, YearsUsed = 4 },
                    new Skill { Name = "TypeScript", Level = SkillLevel.Advanced, YearsUsed = 3 },
                    new Skill { Name = "iOS/Swift", Level = SkillLevel.Intermediate, YearsUsed = 2 },
                    new Skill { Name = "Android/Kotlin", Level = SkillLevel.Intermediate, YearsUsed = 2 },
                    new Skill { Name = "Firebase", Level = SkillLevel.Advanced, YearsUsed = 3 }
                },
                Certifications = new List<string> { "Google Associate Android Developer" },
                Availability = AvailabilityStatus.Available,
                Location = "Remote",
                Bio = "Mobile developer passionate about creating seamless cross-platform experiences.",
                PreferredProjectTypes = new List<string> { "Mobile Apps", "Cross-platform Development" }
            }
        };
    }

    private static List<Project> GetSampleProjects()
    {
        return new List<Project>
        {
            new Project
            {
                Name = "E-Commerce Platform Modernization",
                Description = "Migrate legacy e-commerce platform to Azure cloud with microservices architecture. Implement new features including AI-powered product recommendations and real-time inventory management.",
                RequiredSkills = new List<RequiredSkill>
                {
                    new RequiredSkill { Name = "C#", MinimumLevel = SkillLevel.Advanced, Required = true },
                    new RequiredSkill { Name = "Azure", MinimumLevel = SkillLevel.Advanced, Required = true },
                    new RequiredSkill { Name = "React", MinimumLevel = SkillLevel.Intermediate, Required = true },
                    new RequiredSkill { Name = "Microservices", MinimumLevel = SkillLevel.Intermediate, Required = false }
                },
                TechStack = new List<string> { "C#", ".NET 8", "Azure", "React", "SQL Server", "Redis", "Docker" },
                TeamSize = 5,
                Duration = "6 months",
                StartDate = DateTime.UtcNow.AddDays(14),
                Priority = ProjectPriority.High,
                ProjectType = "Cloud Migration",
                Client = "RetailCorp",
                Status = ProjectStatus.Planning
            },
            new Project
            {
                Name = "AI Customer Service Chatbot",
                Description = "Build an intelligent chatbot using Azure OpenAI and Cognitive Services to handle customer inquiries, with integration to existing CRM system.",
                RequiredSkills = new List<RequiredSkill>
                {
                    new RequiredSkill { Name = "Python", MinimumLevel = SkillLevel.Advanced, Required = true },
                    new RequiredSkill { Name = "Azure ML", MinimumLevel = SkillLevel.Intermediate, Required = true },
                    new RequiredSkill { Name = "NLP", MinimumLevel = SkillLevel.Intermediate, Required = false }
                },
                TechStack = new List<string> { "Python", "Azure OpenAI", "Azure Bot Service", "Cosmos DB", "Azure Functions" },
                TeamSize = 3,
                Duration = "3 months",
                StartDate = DateTime.UtcNow.AddDays(7),
                Priority = ProjectPriority.Medium,
                ProjectType = "AI/ML Projects",
                Client = "TechServices Inc",
                Status = ProjectStatus.Planning
            },
            new Project
            {
                Name = "Real-time Analytics Dashboard",
                Description = "Create a real-time analytics dashboard for monitoring business KPIs with data from multiple sources. Include predictive analytics capabilities.",
                RequiredSkills = new List<RequiredSkill>
                {
                    new RequiredSkill { Name = "Python", MinimumLevel = SkillLevel.Advanced, Required = true },
                    new RequiredSkill { Name = "Azure Data Factory", MinimumLevel = SkillLevel.Intermediate, Required = true },
                    new RequiredSkill { Name = "Power BI", MinimumLevel = SkillLevel.Advanced, Required = true },
                    new RequiredSkill { Name = "React", MinimumLevel = SkillLevel.Intermediate, Required = false }
                },
                TechStack = new List<string> { "Python", "Azure Synapse", "Power BI", "React", "Azure Data Factory" },
                TeamSize = 4,
                Duration = "4 months",
                StartDate = DateTime.UtcNow.AddDays(30),
                Priority = ProjectPriority.Medium,
                ProjectType = "Data Analytics",
                Client = "FinanceGroup",
                Status = ProjectStatus.Planning
            },
            new Project
            {
                Name = "Mobile Banking App",
                Description = "Develop a secure mobile banking application with biometric authentication, real-time transactions, and personal finance management features.",
                RequiredSkills = new List<RequiredSkill>
                {
                    new RequiredSkill { Name = "React Native", MinimumLevel = SkillLevel.Advanced, Required = true },
                    new RequiredSkill { Name = "TypeScript", MinimumLevel = SkillLevel.Intermediate, Required = true },
                    new RequiredSkill { Name = "Security", MinimumLevel = SkillLevel.Advanced, Required = true },
                    new RequiredSkill { Name = "Azure", MinimumLevel = SkillLevel.Intermediate, Required = false }
                },
                TechStack = new List<string> { "React Native", "TypeScript", "Azure AD B2C", "Azure Functions", "Cosmos DB" },
                TeamSize = 4,
                Duration = "8 months",
                StartDate = DateTime.UtcNow.AddDays(21),
                Priority = ProjectPriority.Critical,
                ProjectType = "Mobile Apps",
                Client = "BankMX",
                Status = ProjectStatus.Planning
            }
        };
    }
}
