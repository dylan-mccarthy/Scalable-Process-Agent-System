using System.Runtime.CompilerServices;
using Azure.AI.Inference;
using Microsoft.Extensions.AI;
using AzureChatRole = Azure.AI.Inference.ChatRole;
using ExtensionsChatRole = Microsoft.Extensions.AI.ChatRole;

namespace Node.Runtime.Services;

/// <summary>
/// Implements IChatClient for Azure AI Foundry ChatCompletionsClient.
/// This adapter allows using Azure AI Foundry models with the Microsoft Agent Framework.
/// </summary>
public sealed class AzureAIFoundryChatClient : IChatClient
{
    private readonly ChatCompletionsClient _client;
    private readonly string _modelId;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureAIFoundryChatClient"/> class.
    /// </summary>
    /// <param name="client">The Azure AI Inference chat completions client.</param>
    /// <param name="modelId">The model deployment ID.</param>
    public AzureAIFoundryChatClient(ChatCompletionsClient client, string modelId)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _modelId = modelId ?? throw new ArgumentNullException(nameof(modelId));
    }

    /// <inheritdoc/>
    public ChatClientMetadata Metadata => new("AzureAIFoundry", null, _modelId);

    /// <inheritdoc/>
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // Convert Microsoft.Extensions.AI messages to Azure.AI.Inference format
        var requestMessages = chatMessages
            .Select(ConvertToRequestMessage)
            .ToList();

        var completionOptions = new ChatCompletionsOptions
        {
            Messages = requestMessages,
            Model = _modelId
        };

        // Apply options if provided
        ApplyChatOptions(completionOptions, options);

        // Call Azure AI Foundry API
        var response = await _client.CompleteAsync(completionOptions, cancellationToken);
        
        // Extract content from the response
        // The ChatCompletions response has a Content property at the top level
        var chatResponse = new ChatResponse(new[]
        {
            new ChatMessage(ExtensionsChatRole.Assistant, response.Value.Content)
        });

        return chatResponse;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Convert Microsoft.Extensions.AI messages to Azure.AI.Inference format
        var requestMessages = chatMessages
            .Select(ConvertToRequestMessage)
            .ToList();

        var completionOptions = new ChatCompletionsOptions
        {
            Messages = requestMessages,
            Model = _modelId
        };

        // Apply options if provided
        ApplyChatOptions(completionOptions, options);

        // Call Azure AI Foundry streaming API
        var response = await _client.CompleteStreamingAsync(completionOptions, cancellationToken);

        await foreach (var update in response.WithCancellation(cancellationToken))
        {
            if (update.ContentUpdate != null)
            {
                yield return new ChatResponseUpdate
                {
                    Role = ExtensionsChatRole.Assistant,
                    Contents = [new TextContent(update.ContentUpdate)]
                };
            }
        }
    }

    /// <inheritdoc/>
    public object? GetService(Type serviceType, object? serviceKey = null)
        => serviceType.IsInstanceOfType(this) ? this : null;

    /// <inheritdoc/>
    public void Dispose()
    {
        // ChatCompletionsClient doesn't implement IDisposable
    }

    private static ChatRequestMessage ConvertToRequestMessage(ChatMessage message)
    {
        return message.Role.Value switch
        {
            "system" => new ChatRequestSystemMessage(message.Text ?? string.Empty),
            "user" => new ChatRequestUserMessage(message.Text ?? string.Empty),
            "assistant" => new ChatRequestAssistantMessage(message.Text ?? string.Empty),
            _ => new ChatRequestUserMessage(message.Text ?? string.Empty)
        };
    }

    private static void ApplyChatOptions(ChatCompletionsOptions completionOptions, ChatOptions? options)
    {
        if (options == null) return;

        if (options.Temperature.HasValue)
        {
            completionOptions.Temperature = (float?)options.Temperature.Value;
        }
        if (options.MaxOutputTokens.HasValue)
        {
            completionOptions.MaxTokens = options.MaxOutputTokens.Value;
        }
        if (options.TopP.HasValue)
        {
            completionOptions.NucleusSamplingFactor = (float?)options.TopP.Value;
        }
    }
}
