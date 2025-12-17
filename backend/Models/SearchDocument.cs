using System.Text.Json.Serialization;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;

namespace AlejanBros.Models;

public class EmployeeSearchDocument
{
    [SimpleField(IsKey = true, IsFilterable = true)]
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [SearchableField(IsFilterable = true, IsSortable = true)]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [SearchableField(IsFilterable = true)]
    [JsonPropertyName("department")]
    public string Department { get; set; } = string.Empty;

    [SearchableField(IsFilterable = true)]
    [JsonPropertyName("jobTitle")]
    public string JobTitle { get; set; } = string.Empty;

    [SearchableField(IsFilterable = true, IsFacetable = true)]
    [JsonPropertyName("skills")]
    public string[] Skills { get; set; } = Array.Empty<string>();

    [SimpleField(IsFilterable = true, IsSortable = true)]
    [JsonPropertyName("yearsOfExperience")]
    public int YearsOfExperience { get; set; }

    [SearchableField(IsFilterable = true, IsFacetable = true)]
    [JsonPropertyName("certifications")]
    public string[] Certifications { get; set; } = Array.Empty<string>();

    [SimpleField(IsFilterable = true)]
    [JsonPropertyName("availability")]
    public string Availability { get; set; } = string.Empty;

    [SearchableField(IsFilterable = true)]
    [JsonPropertyName("location")]
    public string Location { get; set; } = string.Empty;

    [SearchableField]
    [JsonPropertyName("bio")]
    public string Bio { get; set; } = string.Empty;

    [SearchableField]
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [VectorSearchField(VectorSearchDimensions = 1536, VectorSearchProfileName = "vector-profile")]
    [JsonPropertyName("embedding")]
    public float[]? Embedding { get; set; }
}
