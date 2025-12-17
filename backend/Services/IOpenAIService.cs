using AlejanBros.Models;

namespace AlejanBros.Services;

public enum QueryType
{
    MatchRequest,      // "Find React developers", "Build a team for..."
    EmployeeQuestion,  // "What is María's availability?", "Tell me about Carlos"
    FollowUp,          // "Why is she ranked higher?", "What about the second one?"
    OffTopic           // "What's the weather?"
}

public class ConversationMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public interface IOpenAIService
{
    Task<float[]> GenerateEmbeddingAsync(string text);
    Task<string> GenerateMatchAnalysisAsync(MatchRequest request, IEnumerable<Employee> candidates, List<ConversationMessage>? history = null);
    Task<MatchResponse> AnalyzeAndRankCandidatesAsync(MatchRequest request, IEnumerable<Employee> candidates);
    Task<bool> IsMatchingRelatedQueryAsync(string message);
    Task<QueryType> ClassifyQueryTypeAsync(string message);
    Task<string> AnswerEmployeeQuestionAsync(string question, IEnumerable<Employee> employees, List<ConversationMessage>? history = null);
}
