using AlejanBros.Application.DTOs;
using AlejanBros.Domain.Entities;
using AlejanBros.Domain.Enums;

namespace AlejanBros.Application.Interfaces;

public interface IAIMatchingService
{
    Task<MatchResponseDto> AnalyzeAndRankCandidatesAsync(
        MatchRequestDto request, 
        IEnumerable<Employee> candidates);
    
    Task<AvailabilityConstraint> ExtractAvailabilityConstraintAsync(string query);
    
    Task<string> AnswerEmployeeQuestionAsync(
        string question, 
        IEnumerable<Employee> employees, 
        List<ConversationMessage>? history = null);
    
    Task<string> GenerateSocialResponseAsync(string message);
}

public class ConversationMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
