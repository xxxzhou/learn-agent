using LearnAgent.Models;

namespace LearnAgent.Clients;

/// <summary>
/// Anthropic Claude 客户端 (预留扩展)
/// </summary>
public class AnthropicClient : ILLMClient
{
    public string Name => "Anthropic Claude";
    
    public AnthropicClient(string apiKey)
    {
        // TODO: 实现 Anthropic API 调用
        throw new NotImplementedException("Anthropic client not implemented yet");
    }
    
    public Task<ChatResponse> ChatAsync(ChatRequest request)
    {
        throw new NotImplementedException();
    }
    
    public void Dispose()
    {
    }
}
