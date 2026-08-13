namespace WorldEngine.Domain.AI;

public sealed class LlmPromptRequest
{
    public LlmPromptRequest(string systemPrompt, string userPrompt, string modelName, string promptVersion)
    {
        SystemPrompt = systemPrompt;
        UserPrompt = userPrompt;
        ModelName = modelName;
        PromptVersion = promptVersion;
    }

    public string SystemPrompt { get; }

    public string UserPrompt { get; }

    public string ModelName { get; }

    public string PromptVersion { get; }
}

public sealed class LlmPromptResponse
{
    public LlmPromptResponse(string responseText, string modelName, int? latencyMs)
    {
        ResponseText = responseText;
        ModelName = modelName;
        LatencyMs = latencyMs;
    }

    public string ResponseText { get; }

    public string ModelName { get; }

    public int? LatencyMs { get; }
}

public interface ILLMClient
{
    Task<LlmPromptResponse> CompleteAsync(LlmPromptRequest request, CancellationToken cancellationToken);
}