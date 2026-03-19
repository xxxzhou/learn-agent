namespace LearnAgent.Models;

/// <summary>
/// 任务项
/// </summary>
public class TodoItem
{
    /// <summary>
    /// 任务ID
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// 任务内容
    /// </summary>
    public string Text { get; set; } = string.Empty;
    
    /// <summary>
    /// 任务状态: pending, in_progress, completed
    /// </summary>
    public string Status { get; set; } = "pending";
}

/// <summary>
/// 任务状态枚举
/// </summary>
public static class TodoStatus
{
    public const string Pending = "pending";
    public const string InProgress = "in_progress";
    public const string Completed = "completed";
    
    private static readonly string[] ValidStatuses = [Pending, InProgress, Completed];
    
    public static bool IsValid(string status)
    {
        return ValidStatuses.Contains(status.ToLowerInvariant());
    }
}
