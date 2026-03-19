using System.Text.Json.Serialization;

namespace LearnAgent.Models;

/// <summary>
/// 聊天响应
/// </summary>
public class ChatResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
    
    [JsonPropertyName("model")]
    public string Model { get; set; } = "";
    
    [JsonPropertyName("choices")]
    public List<Choice> Choices { get; set; } = new();
    
    [JsonPropertyName("usage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Usage? Usage { get; set; }
}

public class Choice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }
    
    [JsonPropertyName("message")]
    public ChatMessage Message { get; set; } = new();
    
    [JsonPropertyName("finish_reason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FinishReason { get; set; }
}

public class Usage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }
    
    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }
    
    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}
