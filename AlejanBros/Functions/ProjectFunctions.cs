using AlejanBros.Models;
using AlejanBros.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AlejanBros.Functions;

public class ProjectFunctions
{
    private readonly ICosmosDbService _cosmosDbService;
    private readonly ILogger<ProjectFunctions> _logger;

    public ProjectFunctions(
        ICosmosDbService cosmosDbService,
        ILogger<ProjectFunctions> logger)
    {
        _cosmosDbService = cosmosDbService;
        _logger = logger;
    }

    [Function("GetProjects")]
    public async Task<IActionResult> GetProjects(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "projects")] HttpRequest req)
    {
        _logger.LogInformation("Getting all projects");

        try
        {
            var projects = await _cosmosDbService.GetAllProjectsAsync();
            return new OkObjectResult(projects);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting projects");
            return new StatusCodeResult(500);
        }
    }

    [Function("GetProject")]
    public async Task<IActionResult> GetProject(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "projects/{id}")] HttpRequest req,
        string id)
    {
        _logger.LogInformation("Getting project with ID: {Id}", id);

        try
        {
            var project = await _cosmosDbService.GetProjectAsync(id);
            if (project == null)
            {
                return new NotFoundObjectResult(new { message = $"Project with ID {id} not found" });
            }
            return new OkObjectResult(project);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting project {Id}", id);
            return new StatusCodeResult(500);
        }
    }

    [Function("CreateProject")]
    public async Task<IActionResult> CreateProject(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "projects")] HttpRequest req)
    {
        _logger.LogInformation("Creating new project");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var project = JsonSerializer.Deserialize<Project>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (project == null)
            {
                return new BadRequestObjectResult(new { message = "Invalid project data" });
            }

            var createdProject = await _cosmosDbService.CreateProjectAsync(project);
            return new CreatedResult($"/api/projects/{createdProject.Id}", createdProject);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Invalid JSON in request body");
            return new BadRequestObjectResult(new { message = "Invalid JSON format" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating project");
            return new StatusCodeResult(500);
        }
    }

    [Function("UpdateProject")]
    public async Task<IActionResult> UpdateProject(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "projects/{id}")] HttpRequest req,
        string id)
    {
        _logger.LogInformation("Updating project with ID: {Id}", id);

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var project = JsonSerializer.Deserialize<Project>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (project == null)
            {
                return new BadRequestObjectResult(new { message = "Invalid project data" });
            }

            project.Id = id;
            var updatedProject = await _cosmosDbService.UpdateProjectAsync(project);
            return new OkObjectResult(updatedProject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating project {Id}", id);
            return new StatusCodeResult(500);
        }
    }

    [Function("DeleteProject")]
    public async Task<IActionResult> DeleteProject(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "projects/{id}")] HttpRequest req,
        string id)
    {
        _logger.LogInformation("Deleting project with ID: {Id}", id);

        try
        {
            await _cosmosDbService.DeleteProjectAsync(id);
            return new NoContentResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting project {Id}", id);
            return new StatusCodeResult(500);
        }
    }
}
