using AlejanBros.Models;

namespace AlejanBros.Services;

public interface IOpenAIService
{
    Task<float[]> GenerateEmbeddingAsync(string text);
    Task<string> GenerateMatchAnalysisAsync(MatchRequest request, IEnumerable<Employee> candidates);
    Task<MatchResponse> AnalyzeAndRankCandidatesAsync(MatchRequest request, IEnumerable<Employee> candidates);
}
