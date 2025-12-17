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

public class EmployeeFunctions
{
    private readonly ICosmosDbService _cosmosDbService;
    private readonly ISearchService _searchService;
    private readonly ILogger<EmployeeFunctions> _logger;

    public EmployeeFunctions(
        ICosmosDbService cosmosDbService,
        ISearchService searchService,
        ILogger<EmployeeFunctions> logger)
    {
        _cosmosDbService = cosmosDbService;
        _searchService = searchService;
        _logger = logger;
    }

    [Function("GetEmployees")]
    [OpenApiOperation(operationId: "GetEmployees", tags: new[] { "Employees" }, Summary = "Get all employees", Description = "Returns a list of all employees in the system")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(IEnumerable<Employee>), Description = "List of employees")]
    public async Task<IActionResult> GetEmployees(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "employees")] HttpRequest req)
    {
        _logger.LogInformation("Getting all employees");

        try
        {
            var employees = await _cosmosDbService.GetAllEmployeesAsync();
            return new OkObjectResult(employees);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting employees");
            return new StatusCodeResult(500);
        }
    }

    [Function("GetEmployee")]
    [OpenApiOperation(operationId: "GetEmployee", tags: new[] { "Employees" }, Summary = "Get employee by ID", Description = "Returns a single employee by their ID")]
    [OpenApiParameter(name: "id", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "Employee ID")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(Employee), Description = "Employee found")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.NotFound, contentType: "application/json", bodyType: typeof(object), Description = "Employee not found")]
    public async Task<IActionResult> GetEmployee(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "employees/{id}")] HttpRequest req,
        string id)
    {
        _logger.LogInformation("Getting employee with ID: {Id}", id);

        try
        {
            var employee = await _cosmosDbService.GetEmployeeAsync(id);
            if (employee == null)
            {
                return new NotFoundObjectResult(new { message = $"Employee with ID {id} not found" });
            }
            return new OkObjectResult(employee);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting employee {Id}", id);
            return new StatusCodeResult(500);
        }
    }

    [Function("CreateEmployee")]
    [OpenApiOperation(operationId: "CreateEmployee", tags: new[] { "Employees" }, Summary = "Create a new employee", Description = "Creates a new employee and indexes them for search")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(Employee), Required = true, Description = "Employee data")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(Employee), Description = "Employee created")]
    public async Task<IActionResult> CreateEmployee(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "employees")] HttpRequest req)
    {
        _logger.LogInformation("Creating new employee");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var employee = JsonSerializer.Deserialize<Employee>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (employee == null)
            {
                return new BadRequestObjectResult(new { message = "Invalid employee data" });
            }

            var createdEmployee = await _cosmosDbService.CreateEmployeeAsync(employee);

            // Index in Azure AI Search
            await _searchService.IndexEmployeeAsync(createdEmployee);

            return new CreatedResult($"/api/employees/{createdEmployee.Id}", createdEmployee);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Invalid JSON in request body");
            return new BadRequestObjectResult(new { message = "Invalid JSON format" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating employee");
            return new StatusCodeResult(500);
        }
    }

    [Function("UpdateEmployee")]
    [OpenApiOperation(operationId: "UpdateEmployee", tags: new[] { "Employees" }, Summary = "Update an employee", Description = "Updates an existing employee by ID")]
    [OpenApiParameter(name: "id", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "Employee ID")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(Employee), Required = true, Description = "Updated employee data")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(Employee), Description = "Employee updated")]
    public async Task<IActionResult> UpdateEmployee(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "employees/{id}")] HttpRequest req,
        string id)
    {
        _logger.LogInformation("Updating employee with ID: {Id}", id);

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var employee = JsonSerializer.Deserialize<Employee>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (employee == null)
            {
                return new BadRequestObjectResult(new { message = "Invalid employee data" });
            }

            employee.Id = id;
            var updatedEmployee = await _cosmosDbService.UpdateEmployeeAsync(employee);

            // Re-index in Azure AI Search
            await _searchService.IndexEmployeeAsync(updatedEmployee);

            return new OkObjectResult(updatedEmployee);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating employee {Id}", id);
            return new StatusCodeResult(500);
        }
    }

    [Function("DeleteEmployee")]
    [OpenApiOperation(operationId: "DeleteEmployee", tags: new[] { "Employees" }, Summary = "Delete an employee", Description = "Deletes an employee by ID")]
    [OpenApiParameter(name: "id", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "Employee ID")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Employee deleted")]
    public async Task<IActionResult> DeleteEmployee(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "employees/{id}")] HttpRequest req,
        string id)
    {
        _logger.LogInformation("Deleting employee with ID: {Id}", id);

        try
        {
            await _cosmosDbService.DeleteEmployeeAsync(id);
            await _searchService.DeleteEmployeeFromIndexAsync(id);
            return new NoContentResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting employee {Id}", id);
            return new StatusCodeResult(500);
        }
    }
}
