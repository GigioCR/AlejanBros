using AlejanBros.Application.Interfaces;
using AlejanBros.Configuration;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.ClientModel;

namespace AlejanBros.Infrastructure.AI.OpenAI;

public class EmbeddingService : IEmbeddingService
{
    private readonly AzureOpenAIClient _client;
    private readonly OpenAISettings _settings;
    private readonly ILogger<EmbeddingService> _logger;

    public EmbeddingService(
        AzureOpenAIClient client,
        IOptions<AzureSettings> settings,
        ILogger<EmbeddingService> logger)
    {
        _client = client;
        _settings = settings.Value.OpenAI;
        _logger = logger;
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        try
        {
            var embeddingClient = _client.GetEmbeddingClient(_settings.EmbeddingDeployment);
            var response = await embeddingClient.GenerateEmbeddingAsync(text);
            return response.Value.ToFloats().ToArray();
        }
        catch (ClientResultException ex) when (ex.Status == 429)
        {
            _logger.LogWarning(ex, "Rate limit exceeded for embedding generation");
            throw new InvalidOperationException("Service is experiencing high demand. Please try again in a moment.", ex);
        }
        catch (ClientResultException ex) when (ex.Status >= 500)
        {
            _logger.LogError(ex, "OpenAI service error during embedding generation");
            throw new InvalidOperationException("AI service is temporarily unavailable. Please try again.", ex);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Timeout generating embedding");
            throw new InvalidOperationException("Request timed out. Please try again.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error generating embedding");
            throw new InvalidOperationException("Unable to process request. Please try again.", ex);
        }
    }
}
