namespace LearnAgent.Models;

/// <summary>
/// 子代理任务请求
/// </summary>
public class SubagentTask
{
    /// <summary>
    /// 任务描述
    /// </summary>
    public string Prompt { get; set; } = string.Empty;
    
    /// <summary>
    /// 简短描述（用于日志）
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// 最大轮数限制（默认 30）
    /// </summary>
    public int MaxRounds { get; set; } = 30;
}

/// <summary>
/// 子代理执行结果
/// </summary>
public class SubagentResult
{
    /// <summary>
    /// 执行是否成功
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// 结果摘要
    /// </summary>
    public string Summary { get; set; } = string.Empty;
    
    /// <summary>
    /// 执行的轮数
    /// </summary>
    public int RoundsExecuted { get; set; }
    
    /// <summary>
    /// 错误信息（如果有）
    /// </summary>
    public string? Error { get; set; }
}
