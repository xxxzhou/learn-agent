using System.Text.Json.Serialization;

namespace LearnAgent.Models;

/// <summary>
/// 工具调用
/// </summary>
public class ToolCall
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
    
    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";
    
    [JsonPropertyName("function")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FunctionCall? Function { get; set; }
}

public class FunctionCall
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
    
    [JsonPropertyName("arguments")]
    public string Arguments { get; set; } = "";
}
