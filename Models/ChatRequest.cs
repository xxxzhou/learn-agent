using System.Text.Json.Serialization;

namespace LearnAgent.Models;

/// <summary>
/// 聊天请求
/// </summary>
public class ChatRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "";
    
    [JsonPropertyName("messages")]
    public List<ChatMessage> Messages { get; set; } = new();
    
    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; } = 1024;
    
    [JsonPropertyName("tools")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ToolDefinition>? Tools { get; set; }
}
