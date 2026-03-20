namespace LearnAgent.Models;

/// <summary>
/// 文件任务状态常量
/// </summary>
public static class FileTaskStatus
{
    public const string Pending = "pending";
    public const string InProgress = "in_progress";
    public const string Completed = "completed";

    public static bool IsValid(string status) =>
        status == Pending || status == InProgress || status == Completed;
}

/// <summary>
/// 持久化任务项 - 支持依赖关系的文件任务
/// </summary>
public class TaskItem
{
    /// <summary>
    /// 任务ID
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// 任务主题/标题
    /// </summary>
    public string Subject { get; set; } = "";
    
    /// <summary>
    /// 任务描述
    /// </summary>
    public string Description { get; set; } = "";
    
    /// <summary>
    /// 任务状态: pending, in_progress, completed
    /// </summary>
    public string Status { get; set; } = FileTaskStatus.Pending;
    
    /// <summary>
    /// 任务负责人
    /// </summary>
    public string Owner { get; set; } = "";
    
    /// <summary>
    /// 被哪些任务阻塞（依赖的任务ID列表）
    /// </summary>
    public List<int> BlockedBy { get; set; } = [];
    
    /// <summary>
    /// 阻塞哪些任务（当前任务完成时解锁的任务ID列表）
    /// </summary>
    public List<int> Blocks { get; set; } = [];
    
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
