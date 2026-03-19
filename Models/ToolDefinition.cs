using System.Text.Json.Serialization;

namespace LearnAgent.Models;

/// <summary>
/// 工具定义
/// </summary>
public class ToolDefinition
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";
    
    [JsonPropertyName("function")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FunctionDefinition? Function { get; set; }
}

public class FunctionDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
    
    [JsonPropertyName("description")]
    public string Description { get; set; } = "";
    
    [JsonPropertyName("parameters")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Parameters { get; set; }
}
