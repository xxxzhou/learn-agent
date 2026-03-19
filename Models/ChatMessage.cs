using System.Text.Json.Serialization;

namespace LearnAgent.Models;

/// <summary>
/// 聊天消息
/// </summary>
public class ChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";
    
    [JsonPropertyName("content")]
    public object? Content { get; set; }
    
    [JsonPropertyName("tool_calls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ToolCall>? ToolCalls { get; set; }
    
    [JsonPropertyName("tool_call_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ToolCallId { get; set; }
}
