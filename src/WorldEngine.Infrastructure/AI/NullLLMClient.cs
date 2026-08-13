using Microsoft.Extensions.Logging;
using WorldEngine.Domain.AI;

namespace WorldEngine.Infrastructure.AI;

public sealed class NullLLMClient : ILLMClient
{
    private readonly ILogger<NullLLMClient>? _logger;

    public NullLLMClient(ILogger<NullLLMClient>? logger = null)
    {
        _logger = logger;
    }

    public Task<LlmPromptResponse> CompleteAsync(LlmPromptRequest request, CancellationToken cancellationToken)
    {
        _logger?.LogDebug("NullLLMClient invoked. Default simulation does not call external LLM providers.");
        var fallbackJson = "{\"actionId\":\"Idle\",\"reason\":\"No LLM provider configured\"}";
        return Task.FromResult(new LlmPromptResponse(fallbackJson, request.ModelName, latencyMs: 0));
    }
}