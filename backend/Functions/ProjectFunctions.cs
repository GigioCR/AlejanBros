using AlejanBros.Models;
using AlejanBros.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using System.Net;
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
    [OpenApiOperation(operationId: "GetProjects", tags: new[] { "Projects" }, Summary = "Get projects with pagination", Description = "Returns a paginated list of projects")]
    [OpenApiParameter(name: "page", In = ParameterLocation.Query, Required = false, Type = typeof(int), Description = "Page number (default: 1)")]
    [OpenApiParameter(name: "pageSize", In = ParameterLocation.Query, Required = false, Type = typeof(int), Description = "Items per page (default: 10, max: 50)")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(PaginatedResult<Project>), Description = "Paginated list of projects")]
    public async Task<IActionResult> GetProjects(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projects")] HttpRequest req)
    {
        _logger.LogInformation("Getting projects with pagination");

        try
        {
            var page = int.TryParse(req.Query["page"], out var p) ? Math.Max(1, p) : 1;
            var pageSize = int.TryParse(req.Query["pageSize"], out var ps) ? Math.Clamp(ps, 1, 50) : 10;

            var result = await _cosmosDbService.GetProjectsAsync(page, pageSize);
            return new OkObjectResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting projects");
            return new StatusCodeResult(500);
        }
    }

    [Function("GetProject")]
    [OpenApiOperation(operationId: "GetProject", tags: new[] { "Projects" }, Summary = "Get project by ID", Description = "Returns a single project by ID")]
    [OpenApiParameter(name: "id", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "Project ID")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(Project), Description = "Project found")]
    public async Task<IActionResult> GetProject(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "projects/{id}")] HttpRequest req,
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
    [OpenApiOperation(operationId: "CreateProject", tags: new[] { "Projects" }, Summary = "Create a new project", Description = "Creates a new project")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(Project), Required = true, Description = "Project data")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(Project), Description = "Project created")]
    public async Task<IActionResult> CreateProject(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projects")] HttpRequest req)
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
    [OpenApiOperation(operationId: "UpdateProject", tags: new[] { "Projects" }, Summary = "Update a project", Description = "Updates an existing project by ID")]
    [OpenApiParameter(name: "id", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "Project ID")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(Project), Required = true, Description = "Updated project data")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(Project), Description = "Project updated")]
    public async Task<IActionResult> UpdateProject(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "projects/{id}")] HttpRequest req,
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
    [OpenApiOperation(operationId: "DeleteProject", tags: new[] { "Projects" }, Summary = "Delete a project", Description = "Deletes a project by ID")]
    [OpenApiParameter(name: "id", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "Project ID")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Project deleted")]
    public async Task<IActionResult> DeleteProject(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "projects/{id}")] HttpRequest req,
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
