using AlejanBros.Models;
using AlejanBros.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using System.Linq;
using AlejanBros.Application.Interfaces;

namespace AlejanBros.Functions;

public class AdminFunctions
{
    private readonly ICosmosDbService _cosmosDbService;
    private readonly ISearchService _searchService;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<AdminFunctions> _logger;

    public AdminFunctions(
        ICosmosDbService cosmosDbService,
        ISearchService searchService,
        IEmbeddingService embeddingService,
        ILogger<AdminFunctions> logger)
    {
        _cosmosDbService = cosmosDbService;
        _searchService = searchService;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    [Function("InitializeSearchIndex")]
    [OpenApiOperation(operationId: "InitializeSearchIndex", tags: new[] { "Admin" }, Summary = "Initialize search index", Description = "Creates or updates the Azure AI Search index")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "Index initialized")]
    public async Task<IActionResult> InitializeSearchIndex(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "setup/initialize-index")] HttpRequest req)
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
    [OpenApiOperation(operationId: "ReindexAllEmployees", tags: new[] { "Admin" }, Summary = "Reindex all employees", Description = "Clears and rebuilds the Azure AI Search index with current Cosmos DB data")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "Reindexing completed")]
    public async Task<IActionResult> ReindexAllEmployees(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "setup/reindex")] HttpRequest req)
    {
        _logger.LogInformation("Clearing and rebuilding search index");

        try
        {
            var employees = await _cosmosDbService.GetAllEmployeesAsync();
            var employeeList = employees.ToList();
            
            // Clear the index and rebuild with fresh data
            await _searchService.ClearAndRebuildIndexAsync(employeeList);

            return new OkObjectResult(new
            {
                message = "Search index cleared and rebuilt successfully",
                employeesIndexed = employeeList.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rebuilding search index");
            return new StatusCodeResult(500);
        }
    }

    [Function("SeedSampleData")]
    [OpenApiOperation(operationId: "SeedSampleData", tags: new[] { "Admin" }, Summary = "Seed sample data", Description = "Seeds the database with sample employees and projects")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "Sample data seeded")]
    public async Task<IActionResult> SeedSampleData(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "setup/seed")] HttpRequest req)
    {
        _logger.LogInformation("Seeding sample data");

        try
        {
            var employees = GetSampleEmployees();
            var projects = GetSampleProjects();

            foreach (var employee in employees)
            {
                var embeddingText = $"{employee.Name} {employee.JobTitle} {employee.Department} {string.Join(" ", employee.Skills.Select(s => s.Name))} {employee.Bio}";
                employee.Embedding = await _embeddingService.GenerateEmbeddingAsync(embeddingText);
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
    [OpenApiOperation(operationId: "HealthCheck", tags: new[] { "Admin" }, Summary = "Health check", Description = "Returns the health status of the API")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "Health status")]
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
            },
            new Employee
            {
                Name = "Sofía Vargas",
                Email = "sofia.vargas@company.com",
                Department = "Engineering",
                JobTitle = "Cloud Architect",
                YearsOfExperience = 10,
                Skills = new List<Skill>
                {
                    new Skill { Name = "Azure", Level = SkillLevel.Expert, YearsUsed = 8 },
                    new Skill { Name = "AWS", Level = SkillLevel.Advanced, YearsUsed = 5 },
                    new Skill { Name = "Terraform", Level = SkillLevel.Expert, YearsUsed = 5 },
                    new Skill { Name = "Kubernetes", Level = SkillLevel.Advanced, YearsUsed = 4 },
                    new Skill { Name = "Security", Level = SkillLevel.Advanced, YearsUsed = 6 }
                },
                Certifications = new List<string> { "Azure Solutions Architect Expert", "AWS Solutions Architect Professional", "HashiCorp Terraform Associate" },
                Availability = AvailabilityStatus.PartiallyAvailable,
                CurrentProjects = new List<string> { "E-Commerce Platform Modernization" },
                Location = "Mexico City",
                Bio = "Cloud architect specializing in multi-cloud strategies and enterprise-grade infrastructure design.",
                PreferredProjectTypes = new List<string> { "Cloud Migration", "Infrastructure", "Enterprise Architecture" }
            },
            new Employee
            {
                Name = "Miguel Ángel Flores",
                Email = "miguel.flores@company.com",
                Department = "Engineering",
                JobTitle = "Junior Developer",
                YearsOfExperience = 1,
                Skills = new List<Skill>
                {
                    new Skill { Name = "JavaScript", Level = SkillLevel.Intermediate, YearsUsed = 1 },
                    new Skill { Name = "React", Level = SkillLevel.Beginner, YearsUsed = 1 },
                    new Skill { Name = "Node.js", Level = SkillLevel.Beginner, YearsUsed = 1 },
                    new Skill { Name = "Git", Level = SkillLevel.Intermediate, YearsUsed = 1 }
                },
                Certifications = new List<string>(),
                Availability = AvailabilityStatus.Available,
                Location = "Puebla",
                Bio = "Enthusiastic junior developer eager to learn and contribute to meaningful projects.",
                PreferredProjectTypes = new List<string> { "Web Applications", "Learning Opportunities" }
            },
            new Employee
            {
                Name = "Isabella Moreno",
                Email = "isabella.moreno@company.com",
                Department = "Data Science",
                JobTitle = "Senior Data Scientist",
                YearsOfExperience = 9,
                Skills = new List<Skill>
                {
                    new Skill { Name = "Python", Level = SkillLevel.Expert, YearsUsed = 8 },
                    new Skill { Name = "R", Level = SkillLevel.Advanced, YearsUsed = 6 },
                    new Skill { Name = "Machine Learning", Level = SkillLevel.Expert, YearsUsed = 7 },
                    new Skill { Name = "Deep Learning", Level = SkillLevel.Advanced, YearsUsed = 4 },
                    new Skill { Name = "Statistics", Level = SkillLevel.Expert, YearsUsed = 9 }
                },
                Certifications = new List<string> { "Google Professional Data Engineer", "AWS Machine Learning Specialty" },
                Availability = AvailabilityStatus.Available,
                CurrentProjects = new List<string> { "AI Customer Service Chatbot" },
                Location = "Guadalajara",
                Bio = "Data scientist with deep expertise in statistical modeling and predictive analytics for business intelligence.",
                PreferredProjectTypes = new List<string> { "AI/ML Projects", "Data Analytics", "Research" }
            },
            new Employee
            {
                Name = "Andrés Castillo",
                Email = "andres.castillo@company.com",
                Department = "Engineering",
                JobTitle = "Security Engineer",
                YearsOfExperience = 7,
                Skills = new List<Skill>
                {
                    new Skill { Name = "Security", Level = SkillLevel.Expert, YearsUsed = 7 },
                    new Skill { Name = "Azure AD", Level = SkillLevel.Expert, YearsUsed = 5 },
                    new Skill { Name = "Penetration Testing", Level = SkillLevel.Advanced, YearsUsed = 4 },
                    new Skill { Name = "Python", Level = SkillLevel.Intermediate, YearsUsed = 3 },
                    new Skill { Name = "Compliance", Level = SkillLevel.Advanced, YearsUsed = 5 }
                },
                Certifications = new List<string> { "CISSP", "CEH", "Azure Security Engineer Associate" },
                Availability = AvailabilityStatus.PartiallyAvailable,
                CurrentProjects = new List<string> { "Mobile Banking App" },
                Location = "Monterrey",
                Bio = "Security engineer focused on protecting enterprise systems and ensuring regulatory compliance.",
                PreferredProjectTypes = new List<string> { "Security Audits", "Compliance", "Infrastructure" }
            },
            new Employee
            {
                Name = "Valentina Reyes",
                Email = "valentina.reyes@company.com",
                Department = "Design",
                JobTitle = "UX/UI Designer",
                YearsOfExperience = 6,
                Skills = new List<Skill>
                {
                    new Skill { Name = "Figma", Level = SkillLevel.Expert, YearsUsed = 5 },
                    new Skill { Name = "Adobe XD", Level = SkillLevel.Advanced, YearsUsed = 4 },
                    new Skill { Name = "User Research", Level = SkillLevel.Advanced, YearsUsed = 5 },
                    new Skill { Name = "Prototyping", Level = SkillLevel.Expert, YearsUsed = 6 },
                    new Skill { Name = "CSS/Tailwind", Level = SkillLevel.Intermediate, YearsUsed = 2 }
                },
                Certifications = new List<string> { "Google UX Design Certificate" },
                Availability = AvailabilityStatus.Available,
                Location = "Remote",
                Bio = "UX/UI designer passionate about creating intuitive and accessible digital experiences.",
                PreferredProjectTypes = new List<string> { "UI/UX Projects", "Mobile Apps", "Web Applications" }
            },
            new Employee
            {
                Name = "Javier Mendoza",
                Email = "javier.mendoza@company.com",
                Department = "Engineering",
                JobTitle = "QA Engineer",
                YearsOfExperience = 5,
                Skills = new List<Skill>
                {
                    new Skill { Name = "Selenium", Level = SkillLevel.Expert, YearsUsed = 4 },
                    new Skill { Name = "Cypress", Level = SkillLevel.Advanced, YearsUsed = 2 },
                    new Skill { Name = "Python", Level = SkillLevel.Intermediate, YearsUsed = 3 },
                    new Skill { Name = "API Testing", Level = SkillLevel.Advanced, YearsUsed = 4 },
                    new Skill { Name = "Performance Testing", Level = SkillLevel.Intermediate, YearsUsed = 2 }
                },
                Certifications = new List<string> { "ISTQB Foundation Level" },
                Availability = AvailabilityStatus.Available,
                CurrentProjects = new List<string> { "E-Commerce Platform Modernization" },
                Location = "Mexico City",
                Bio = "QA engineer dedicated to ensuring software quality through comprehensive testing strategies.",
                PreferredProjectTypes = new List<string> { "Quality Assurance", "Automation", "Web Applications" }
            },
            new Employee
            {
                Name = "Camila Ortiz",
                Email = "camila.ortiz@company.com",
                Department = "Engineering",
                JobTitle = "Full Stack Developer",
                YearsOfExperience = 4,
                Skills = new List<Skill>
                {
                    new Skill { Name = "Node.js", Level = SkillLevel.Advanced, YearsUsed = 4 },
                    new Skill { Name = "React", Level = SkillLevel.Advanced, YearsUsed = 3 },
                    new Skill { Name = "MongoDB", Level = SkillLevel.Advanced, YearsUsed = 3 },
                    new Skill { Name = "TypeScript", Level = SkillLevel.Intermediate, YearsUsed = 2 },
                    new Skill { Name = "GraphQL", Level = SkillLevel.Intermediate, YearsUsed = 2 }
                },
                Certifications = new List<string> { "MongoDB Developer Associate" },
                Availability = AvailabilityStatus.Available,
                Location = "Querétaro",
                Bio = "Full stack developer with a passion for building scalable web applications using modern JavaScript frameworks.",
                PreferredProjectTypes = new List<string> { "Web Applications", "API Development", "Startups" }
            },
            new Employee
            {
                Name = "Sebastián Delgado",
                Email = "sebastian.delgado@company.com",
                Department = "Data Science",
                JobTitle = "Business Intelligence Analyst",
                YearsOfExperience = 4,
                Skills = new List<Skill>
                {
                    new Skill { Name = "Power BI", Level = SkillLevel.Expert, YearsUsed = 4 },
                    new Skill { Name = "SQL", Level = SkillLevel.Advanced, YearsUsed = 4 },
                    new Skill { Name = "Excel", Level = SkillLevel.Expert, YearsUsed = 4 },
                    new Skill { Name = "Python", Level = SkillLevel.Beginner, YearsUsed = 1 },
                    new Skill { Name = "DAX", Level = SkillLevel.Advanced, YearsUsed = 3 }
                },
                Certifications = new List<string> { "Microsoft Power BI Data Analyst Associate" },
                Availability = AvailabilityStatus.Available,
                CurrentProjects = new List<string> { "Real-time Analytics Dashboard" },
                Location = "Guadalajara",
                Bio = "BI analyst skilled in transforming complex data into actionable business insights.",
                PreferredProjectTypes = new List<string> { "BI Solutions", "Data Analytics", "Reporting" }
            },
            new Employee
            {
                Name = "Daniela Jiménez",
                Email = "daniela.jimenez@company.com",
                Department = "Engineering",
                JobTitle = "Site Reliability Engineer",
                YearsOfExperience = 6,
                Skills = new List<Skill>
                {
                    new Skill { Name = "Kubernetes", Level = SkillLevel.Expert, YearsUsed = 4 },
                    new Skill { Name = "Prometheus", Level = SkillLevel.Advanced, YearsUsed = 3 },
                    new Skill { Name = "Grafana", Level = SkillLevel.Advanced, YearsUsed = 3 },
                    new Skill { Name = "Go", Level = SkillLevel.Intermediate, YearsUsed = 2 },
                    new Skill { Name = "Linux", Level = SkillLevel.Expert, YearsUsed = 6 }
                },
                Certifications = new List<string> { "CKA", "AWS SysOps Administrator" },
                Availability = AvailabilityStatus.PartiallyAvailable,
                Location = "Remote",
                Bio = "SRE focused on building reliable, scalable systems with strong observability practices.",
                PreferredProjectTypes = new List<string> { "Infrastructure", "DevOps Transformation", "Cloud Migration" }
            },
            new Employee
            {
                Name = "Ricardo Navarro",
                Email = "ricardo.navarro@company.com",
                Department = "Engineering",
                JobTitle = "Embedded Systems Developer",
                YearsOfExperience = 8,
                Skills = new List<Skill>
                {
                    new Skill { Name = "C", Level = SkillLevel.Expert, YearsUsed = 8 },
                    new Skill { Name = "C++", Level = SkillLevel.Advanced, YearsUsed = 6 },
                    new Skill { Name = "RTOS", Level = SkillLevel.Advanced, YearsUsed = 5 },
                    new Skill { Name = "IoT", Level = SkillLevel.Advanced, YearsUsed = 4 },
                    new Skill { Name = "Python", Level = SkillLevel.Intermediate, YearsUsed = 3 }
                },
                Certifications = new List<string> { "ARM Accredited Engineer" },
                Availability = AvailabilityStatus.Available,
                Location = "Monterrey",
                Bio = "Embedded systems developer with expertise in IoT solutions and real-time operating systems.",
                PreferredProjectTypes = new List<string> { "IoT Projects", "Embedded Systems", "Hardware Integration" }
            },
            new Employee
            {
                Name = "Gabriela Vega",
                Email = "gabriela.vega@company.com",
                Department = "Product",
                JobTitle = "Product Manager",
                YearsOfExperience = 7,
                Skills = new List<Skill>
                {
                    new Skill { Name = "Product Strategy", Level = SkillLevel.Expert, YearsUsed = 6 },
                    new Skill { Name = "Agile/Scrum", Level = SkillLevel.Expert, YearsUsed = 7 },
                    new Skill { Name = "User Research", Level = SkillLevel.Advanced, YearsUsed = 5 },
                    new Skill { Name = "Data Analysis", Level = SkillLevel.Intermediate, YearsUsed = 4 },
                    new Skill { Name = "Jira", Level = SkillLevel.Expert, YearsUsed = 6 }
                },
                Certifications = new List<string> { "Certified Scrum Product Owner", "Pragmatic Marketing Certified" },
                Availability = AvailabilityStatus.PartiallyAvailable,
                CurrentProjects = new List<string> { "Mobile Banking App" },
                Location = "Mexico City",
                Bio = "Product manager with a track record of launching successful digital products in fintech and e-commerce.",
                PreferredProjectTypes = new List<string> { "Product Development", "Digital Transformation", "Startups" }
            },
            new Employee
            {
                Name = "Alejandro Ríos",
                Email = "alejandro.rios@company.com",
                Department = "Engineering",
                JobTitle = "Blockchain Developer",
                YearsOfExperience = 4,
                Skills = new List<Skill>
                {
                    new Skill { Name = "Solidity", Level = SkillLevel.Advanced, YearsUsed = 3 },
                    new Skill { Name = "Ethereum", Level = SkillLevel.Advanced, YearsUsed = 3 },
                    new Skill { Name = "JavaScript", Level = SkillLevel.Advanced, YearsUsed = 4 },
                    new Skill { Name = "Web3.js", Level = SkillLevel.Advanced, YearsUsed = 2 },
                    new Skill { Name = "Smart Contracts", Level = SkillLevel.Advanced, YearsUsed = 3 }
                },
                Certifications = new List<string> { "Certified Blockchain Developer" },
                Availability = AvailabilityStatus.Available,
                Location = "Remote",
                Bio = "Blockchain developer passionate about decentralized applications and Web3 technologies.",
                PreferredProjectTypes = new List<string> { "Blockchain", "DeFi", "Web3" }
            },
            new Employee
            {
                Name = "Mariana Castro",
                Email = "mariana.castro@company.com",
                Department = "Engineering",
                JobTitle = "Technical Writer",
                YearsOfExperience = 5,
                Skills = new List<Skill>
                {
                    new Skill { Name = "Technical Writing", Level = SkillLevel.Expert, YearsUsed = 5 },
                    new Skill { Name = "API Documentation", Level = SkillLevel.Advanced, YearsUsed = 4 },
                    new Skill { Name = "Markdown", Level = SkillLevel.Expert, YearsUsed = 5 },
                    new Skill { Name = "Git", Level = SkillLevel.Intermediate, YearsUsed = 3 },
                    new Skill { Name = "Developer Experience", Level = SkillLevel.Advanced, YearsUsed = 3 }
                },
                Certifications = new List<string>(),
                Availability = AvailabilityStatus.Available,
                Location = "Puebla",
                Bio = "Technical writer dedicated to creating clear, comprehensive documentation for developers.",
                PreferredProjectTypes = new List<string> { "Documentation", "Developer Experience", "API Development" }
            },
            new Employee
            {
                Name = "Héctor Guzmán",
                Email = "hector.guzman@company.com",
                Department = "Engineering",
                JobTitle = "Database Administrator",
                YearsOfExperience = 11,
                Skills = new List<Skill>
                {
                    new Skill { Name = "SQL Server", Level = SkillLevel.Expert, YearsUsed = 10 },
                    new Skill { Name = "PostgreSQL", Level = SkillLevel.Expert, YearsUsed = 7 },
                    new Skill { Name = "MongoDB", Level = SkillLevel.Advanced, YearsUsed = 4 },
                    new Skill { Name = "Performance Tuning", Level = SkillLevel.Expert, YearsUsed = 8 },
                    new Skill { Name = "Backup/Recovery", Level = SkillLevel.Expert, YearsUsed = 10 }
                },
                Certifications = new List<string> { "Microsoft Certified: Azure Database Administrator", "Oracle Database Administrator Certified Professional" },
                Availability = AvailabilityStatus.Available,
                Location = "Mexico City",
                Bio = "Senior DBA with extensive experience in database optimization, high availability, and disaster recovery.",
                PreferredProjectTypes = new List<string> { "Database Migration", "Performance Optimization", "Enterprise Architecture" }
            },
            new Employee
            {
                Name = "Lucía Peña",
                Email = "lucia.pena@company.com",
                Department = "Data Science",
                JobTitle = "NLP Engineer",
                YearsOfExperience = 4,
                Skills = new List<Skill>
                {
                    new Skill { Name = "Python", Level = SkillLevel.Advanced, YearsUsed = 4 },
                    new Skill { Name = "NLP", Level = SkillLevel.Advanced, YearsUsed = 3 },
                    new Skill { Name = "Transformers", Level = SkillLevel.Advanced, YearsUsed = 2 },
                    new Skill { Name = "Azure OpenAI", Level = SkillLevel.Intermediate, YearsUsed = 1 },
                    new Skill { Name = "spaCy", Level = SkillLevel.Advanced, YearsUsed = 3 }
                },
                Certifications = new List<string> { "DeepLearning.AI NLP Specialization" },
                Availability = AvailabilityStatus.Available,
                CurrentProjects = new List<string> { "AI Customer Service Chatbot" },
                Location = "Guadalajara",
                Bio = "NLP engineer specializing in conversational AI and text analytics solutions.",
                PreferredProjectTypes = new List<string> { "AI/ML Projects", "Chatbots", "Text Analytics" }
            },
            new Employee
            {
                Name = "Emilio Salazar",
                Email = "emilio.salazar@company.com",
                Department = "Engineering",
                JobTitle = "Game Developer",
                YearsOfExperience = 6,
                Skills = new List<Skill>
                {
                    new Skill { Name = "Unity", Level = SkillLevel.Expert, YearsUsed = 5 },
                    new Skill { Name = "C#", Level = SkillLevel.Advanced, YearsUsed = 6 },
                    new Skill { Name = "3D Graphics", Level = SkillLevel.Advanced, YearsUsed = 4 },
                    new Skill { Name = "Unreal Engine", Level = SkillLevel.Intermediate, YearsUsed = 2 },
                    new Skill { Name = "VR/AR", Level = SkillLevel.Intermediate, YearsUsed = 2 }
                },
                Certifications = new List<string> { "Unity Certified Developer" },
                Availability = AvailabilityStatus.Available,
                Location = "Remote",
                Bio = "Game developer with experience in mobile games, VR experiences, and interactive simulations.",
                PreferredProjectTypes = new List<string> { "Game Development", "VR/AR", "Interactive Media" }
            },
            new Employee
            {
                Name = "Paula Herrera",
                Email = "paula.herrera@company.com",
                Department = "Engineering",
                JobTitle = "iOS Developer",
                YearsOfExperience = 5,
                Skills = new List<Skill>
                {
                    new Skill { Name = "iOS/Swift", Level = SkillLevel.Expert, YearsUsed = 5 },
                    new Skill { Name = "SwiftUI", Level = SkillLevel.Advanced, YearsUsed = 2 },
                    new Skill { Name = "Objective-C", Level = SkillLevel.Intermediate, YearsUsed = 3 },
                    new Skill { Name = "Core Data", Level = SkillLevel.Advanced, YearsUsed = 4 },
                    new Skill { Name = "CI/CD", Level = SkillLevel.Intermediate, YearsUsed = 2 }
                },
                Certifications = new List<string> { "Apple Certified iOS Developer" },
                Availability = AvailabilityStatus.PartiallyAvailable,
                CurrentProjects = new List<string> { "Mobile Banking App" },
                Location = "Monterrey",
                Bio = "iOS developer focused on creating polished, performant native applications.",
                PreferredProjectTypes = new List<string> { "Mobile Apps", "iOS Development", "Consumer Apps" }
            },
            new Employee
            {
                Name = "Tomás Aguilar",
                Email = "tomas.aguilar@company.com",
                Department = "Engineering",
                JobTitle = "Android Developer",
                YearsOfExperience = 5,
                Skills = new List<Skill>
                {
                    new Skill { Name = "Android/Kotlin", Level = SkillLevel.Expert, YearsUsed = 4 },
                    new Skill { Name = "Java", Level = SkillLevel.Advanced, YearsUsed = 5 },
                    new Skill { Name = "Jetpack Compose", Level = SkillLevel.Advanced, YearsUsed = 2 },
                    new Skill { Name = "Firebase", Level = SkillLevel.Advanced, YearsUsed = 3 },
                    new Skill { Name = "Room Database", Level = SkillLevel.Advanced, YearsUsed = 3 }
                },
                Certifications = new List<string> { "Google Associate Android Developer" },
                Availability = AvailabilityStatus.Available,
                Location = "Querétaro",
                Bio = "Android developer passionate about modern Android development with Kotlin and Jetpack.",
                PreferredProjectTypes = new List<string> { "Mobile Apps", "Android Development", "Consumer Apps" }
            },
            new Employee
            {
                Name = "Elena Campos",
                Email = "elena.campos@company.com",
                Department = "Engineering",
                JobTitle = "Performance Engineer",
                YearsOfExperience = 7,
                Skills = new List<Skill>
                {
                    new Skill { Name = "Performance Testing", Level = SkillLevel.Expert, YearsUsed = 6 },
                    new Skill { Name = "JMeter", Level = SkillLevel.Expert, YearsUsed = 5 },
                    new Skill { Name = "LoadRunner", Level = SkillLevel.Advanced, YearsUsed = 4 },
                    new Skill { Name = "APM Tools", Level = SkillLevel.Advanced, YearsUsed = 5 },
                    new Skill { Name = "Java", Level = SkillLevel.Intermediate, YearsUsed = 4 }
                },
                Certifications = new List<string> { "Certified Performance Testing Professional" },
                Availability = AvailabilityStatus.Available,
                Location = "Mexico City",
                Bio = "Performance engineer specialized in identifying and resolving bottlenecks in enterprise applications.",
                PreferredProjectTypes = new List<string> { "Performance Optimization", "Load Testing", "Enterprise Architecture" }
            },
            new Employee
            {
                Name = "Oscar Medina",
                Email = "oscar.medina@company.com",
                Department = "Engineering",
                JobTitle = "Integration Specialist",
                YearsOfExperience = 8,
                Skills = new List<Skill>
                {
                    new Skill { Name = "MuleSoft", Level = SkillLevel.Expert, YearsUsed = 5 },
                    new Skill { Name = "Azure Integration Services", Level = SkillLevel.Advanced, YearsUsed = 4 },
                    new Skill { Name = "REST APIs", Level = SkillLevel.Expert, YearsUsed = 7 },
                    new Skill { Name = "SOAP", Level = SkillLevel.Advanced, YearsUsed = 6 },
                    new Skill { Name = "Kafka", Level = SkillLevel.Advanced, YearsUsed = 3 }
                },
                Certifications = new List<string> { "MuleSoft Certified Developer", "Azure Integration Services Specialist" },
                Availability = AvailabilityStatus.Available,
                Location = "Guadalajara",
                Bio = "Integration specialist with expertise in connecting enterprise systems and building robust API ecosystems.",
                PreferredProjectTypes = new List<string> { "Integration Projects", "API Development", "Enterprise Architecture" }
            },
            new Employee
            {
                Name = "Renata Silva",
                Email = "renata.silva@company.com",
                Department = "Engineering",
                JobTitle = "Junior Data Analyst",
                YearsOfExperience = 1,
                Skills = new List<Skill>
                {
                    new Skill { Name = "SQL", Level = SkillLevel.Intermediate, YearsUsed = 1 },
                    new Skill { Name = "Excel", Level = SkillLevel.Advanced, YearsUsed = 2 },
                    new Skill { Name = "Python", Level = SkillLevel.Beginner, YearsUsed = 1 },
                    new Skill { Name = "Tableau", Level = SkillLevel.Beginner, YearsUsed = 1 }
                },
                Certifications = new List<string> { "Google Data Analytics Certificate" },
                Availability = AvailabilityStatus.Available,
                Location = "Puebla",
                Bio = "Junior data analyst eager to develop skills in data visualization and business analytics.",
                PreferredProjectTypes = new List<string> { "Data Analytics", "Reporting", "Learning Opportunities" }
            },
            new Employee
            {
                Name = "Arturo Domínguez",
                Email = "arturo.dominguez@company.com",
                Department = "Engineering",
                JobTitle = "SAP Consultant",
                YearsOfExperience = 12,
                Skills = new List<Skill>
                {
                    new Skill { Name = "SAP ABAP", Level = SkillLevel.Expert, YearsUsed = 10 },
                    new Skill { Name = "SAP S/4HANA", Level = SkillLevel.Advanced, YearsUsed = 4 },
                    new Skill { Name = "SAP Fiori", Level = SkillLevel.Advanced, YearsUsed = 3 },
                    new Skill { Name = "SAP Integration", Level = SkillLevel.Expert, YearsUsed = 8 },
                    new Skill { Name = "Business Process", Level = SkillLevel.Expert, YearsUsed = 10 }
                },
                Certifications = new List<string> { "SAP Certified Application Associate", "SAP Certified Development Associate" },
                Availability = AvailabilityStatus.Unavailable,
                Location = "Mexico City",
                Bio = "Senior SAP consultant with extensive experience in ERP implementations and customizations.",
                PreferredProjectTypes = new List<string> { "ERP Implementation", "SAP Migration", "Enterprise Architecture" }
            },
            new Employee
            {
                Name = "Natalia Romero",
                Email = "natalia.romero@company.com",
                Department = "Engineering",
                JobTitle = "Scrum Master",
                YearsOfExperience = 6,
                Skills = new List<Skill>
                {
                    new Skill { Name = "Agile/Scrum", Level = SkillLevel.Expert, YearsUsed = 6 },
                    new Skill { Name = "Jira", Level = SkillLevel.Expert, YearsUsed = 5 },
                    new Skill { Name = "Facilitation", Level = SkillLevel.Expert, YearsUsed = 6 },
                    new Skill { Name = "Coaching", Level = SkillLevel.Advanced, YearsUsed = 4 },
                    new Skill { Name = "Kanban", Level = SkillLevel.Advanced, YearsUsed = 4 }
                },
                Certifications = new List<string> { "Certified Scrum Master", "SAFe Scrum Master", "ICAgile Certified Professional" },
                Availability = AvailabilityStatus.PartiallyAvailable,
                CurrentProjects = new List<string> { "E-Commerce Platform Modernization" },
                Location = "Remote",
                Bio = "Experienced Scrum Master passionate about helping teams achieve high performance through agile practices.",
                PreferredProjectTypes = new List<string> { "Agile Transformation", "Team Coaching", "Process Improvement" }
            },
            new Employee
            {
                Name = "Iván Guerrero",
                Email = "ivan.guerrero@company.com",
                Department = "Engineering",
                JobTitle = "Network Engineer",
                YearsOfExperience = 9,
                Skills = new List<Skill>
                {
                    new Skill { Name = "Cisco", Level = SkillLevel.Expert, YearsUsed = 8 },
                    new Skill { Name = "Network Security", Level = SkillLevel.Advanced, YearsUsed = 6 },
                    new Skill { Name = "Azure Networking", Level = SkillLevel.Advanced, YearsUsed = 4 },
                    new Skill { Name = "SD-WAN", Level = SkillLevel.Intermediate, YearsUsed = 2 },
                    new Skill { Name = "Firewall", Level = SkillLevel.Expert, YearsUsed = 7 }
                },
                Certifications = new List<string> { "CCNP", "Azure Network Engineer Associate" },
                Availability = AvailabilityStatus.Available,
                Location = "Monterrey",
                Bio = "Network engineer with expertise in enterprise networking and cloud connectivity solutions.",
                PreferredProjectTypes = new List<string> { "Infrastructure", "Network Design", "Cloud Migration" }
            },
            new Employee
            {
                Name = "Adriana Fuentes",
                Email = "adriana.fuentes@company.com",
                Department = "Data Science",
                JobTitle = "Computer Vision Engineer",
                YearsOfExperience = 5,
                Skills = new List<Skill>
                {
                    new Skill { Name = "Python", Level = SkillLevel.Advanced, YearsUsed = 5 },
                    new Skill { Name = "OpenCV", Level = SkillLevel.Expert, YearsUsed = 4 },
                    new Skill { Name = "TensorFlow", Level = SkillLevel.Advanced, YearsUsed = 3 },
                    new Skill { Name = "PyTorch", Level = SkillLevel.Advanced, YearsUsed = 3 },
                    new Skill { Name = "YOLO", Level = SkillLevel.Advanced, YearsUsed = 2 }
                },
                Certifications = new List<string> { "Deep Learning Specialization" },
                Availability = AvailabilityStatus.Available,
                Location = "Guadalajara",
                Bio = "Computer vision engineer focused on object detection, image classification, and video analytics.",
                PreferredProjectTypes = new List<string> { "AI/ML Projects", "Computer Vision", "Automation" }
            },
            new Employee
            {
                Name = "Manuel Espinoza",
                Email = "manuel.espinoza@company.com",
                Department = "Engineering",
                JobTitle = "Mid-Level Backend Developer",
                YearsOfExperience = 3,
                Skills = new List<Skill>
                {
                    new Skill { Name = "C#", Level = SkillLevel.Intermediate, YearsUsed = 3 },
                    new Skill { Name = ".NET", Level = SkillLevel.Intermediate, YearsUsed = 3 },
                    new Skill { Name = "Azure Functions", Level = SkillLevel.Intermediate, YearsUsed = 2 },
                    new Skill { Name = "SQL Server", Level = SkillLevel.Intermediate, YearsUsed = 2 },
                    new Skill { Name = "REST APIs", Level = SkillLevel.Intermediate, YearsUsed = 2 }
                },
                Certifications = new List<string> { "Azure Fundamentals" },
                Availability = AvailabilityStatus.Available,
                CurrentProjects = new List<string> { "E-Commerce Platform Modernization" },
                Location = "Mexico City",
                Bio = "Backend developer building skills in cloud-native development with .NET and Azure.",
                PreferredProjectTypes = new List<string> { "API Development", "Cloud Migration", "Web Applications" }
            },
            new Employee
            {
                Name = "Carolina Ibarra",
                Email = "carolina.ibarra@company.com",
                Department = "Engineering",
                JobTitle = "Data Platform Engineer",
                YearsOfExperience = 6,
                Skills = new List<Skill>
                {
                    new Skill { Name = "Snowflake", Level = SkillLevel.Expert, YearsUsed = 3 },
                    new Skill { Name = "dbt", Level = SkillLevel.Advanced, YearsUsed = 2 },
                    new Skill { Name = "Python", Level = SkillLevel.Advanced, YearsUsed = 5 },
                    new Skill { Name = "Airflow", Level = SkillLevel.Advanced, YearsUsed = 3 },
                    new Skill { Name = "SQL", Level = SkillLevel.Expert, YearsUsed = 6 }
                },
                Certifications = new List<string> { "Snowflake SnowPro Core", "dbt Analytics Engineering" },
                Availability = AvailabilityStatus.Available,
                CurrentProjects = new List<string> { "Real-time Analytics Dashboard" },
                Location = "Remote",
                Bio = "Data platform engineer specializing in modern data stack implementations and analytics engineering.",
                PreferredProjectTypes = new List<string> { "Data Analytics", "ETL/ELT Projects", "Data Platform" }
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
            },
            new Project
            {
                Name = "IoT Fleet Management System",
                Description = "Build a comprehensive IoT platform for tracking and managing a fleet of delivery vehicles, including real-time GPS tracking, predictive maintenance, and route optimization.",
                RequiredSkills = new List<RequiredSkill>
                {
                    new RequiredSkill { Name = "IoT", MinimumLevel = SkillLevel.Advanced, Required = true },
                    new RequiredSkill { Name = "Python", MinimumLevel = SkillLevel.Intermediate, Required = true },
                    new RequiredSkill { Name = "Azure", MinimumLevel = SkillLevel.Intermediate, Required = true },
                    new RequiredSkill { Name = "C", MinimumLevel = SkillLevel.Intermediate, Required = false }
                },
                TechStack = new List<string> { "Azure IoT Hub", "Python", "C", "React", "PostgreSQL", "Azure Maps" },
                TeamSize = 5,
                Duration = "10 months",
                StartDate = DateTime.UtcNow.AddDays(45),
                Priority = ProjectPriority.High,
                ProjectType = "IoT Projects",
                Client = "LogiTrans",
                Status = ProjectStatus.Planning
            },
            new Project
            {
                Name = "Healthcare Patient Portal",
                Description = "Create a HIPAA-compliant patient portal for a healthcare network, enabling appointment scheduling, medical records access, telemedicine integration, and secure messaging with providers.",
                RequiredSkills = new List<RequiredSkill>
                {
                    new RequiredSkill { Name = "C#", MinimumLevel = SkillLevel.Advanced, Required = true },
                    new RequiredSkill { Name = "React", MinimumLevel = SkillLevel.Advanced, Required = true },
                    new RequiredSkill { Name = "Security", MinimumLevel = SkillLevel.Advanced, Required = true },
                    new RequiredSkill { Name = "SQL Server", MinimumLevel = SkillLevel.Intermediate, Required = true }
                },
                TechStack = new List<string> { "C#", ".NET 8", "React", "SQL Server", "Azure", "FHIR API" },
                TeamSize = 6,
                Duration = "12 months",
                StartDate = DateTime.UtcNow.AddDays(60),
                Priority = ProjectPriority.Critical,
                ProjectType = "Web Applications",
                Client = "HealthFirst Network",
                Status = ProjectStatus.Planning
            },
            new Project
            {
                Name = "Supply Chain Blockchain Solution",
                Description = "Implement a blockchain-based supply chain tracking system for verifying product authenticity and tracking goods from manufacturer to consumer.",
                RequiredSkills = new List<RequiredSkill>
                {
                    new RequiredSkill { Name = "Solidity", MinimumLevel = SkillLevel.Advanced, Required = true },
                    new RequiredSkill { Name = "JavaScript", MinimumLevel = SkillLevel.Advanced, Required = true },
                    new RequiredSkill { Name = "Smart Contracts", MinimumLevel = SkillLevel.Intermediate, Required = true },
                    new RequiredSkill { Name = "Node.js", MinimumLevel = SkillLevel.Intermediate, Required = false }
                },
                TechStack = new List<string> { "Ethereum", "Solidity", "Web3.js", "Node.js", "React", "IPFS" },
                TeamSize = 4,
                Duration = "6 months",
                StartDate = DateTime.UtcNow.AddDays(30),
                Priority = ProjectPriority.Medium,
                ProjectType = "Blockchain",
                Client = "GlobalSupply Co",
                Status = ProjectStatus.Planning
            },
            new Project
            {
                Name = "Enterprise SAP S/4HANA Migration",
                Description = "Migrate legacy ERP systems to SAP S/4HANA for a manufacturing company, including data migration, custom development, and integration with existing systems.",
                RequiredSkills = new List<RequiredSkill>
                {
                    new RequiredSkill { Name = "SAP ABAP", MinimumLevel = SkillLevel.Advanced, Required = true },
                    new RequiredSkill { Name = "SAP S/4HANA", MinimumLevel = SkillLevel.Intermediate, Required = true },
                    new RequiredSkill { Name = "SAP Integration", MinimumLevel = SkillLevel.Advanced, Required = true },
                    new RequiredSkill { Name = "Business Process", MinimumLevel = SkillLevel.Intermediate, Required = false }
                },
                TechStack = new List<string> { "SAP S/4HANA", "SAP ABAP", "SAP Fiori", "SAP Integration Suite", "Azure" },
                TeamSize = 8,
                Duration = "18 months",
                StartDate = DateTime.UtcNow.AddDays(90),
                Priority = ProjectPriority.High,
                ProjectType = "ERP Implementation",
                Client = "ManufacturePro Industries",
                Status = ProjectStatus.Planning
            }
        };
    }
}
