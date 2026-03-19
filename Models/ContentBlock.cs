namespace LearnAgent.Models;

/// <summary>
/// 内容块（用于 Gemini 响应解析）
/// </summary>
public class ContentBlock
{
    public string Type { get; set; } = "text";
    public string? Text { get; set; }
}
