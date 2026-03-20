namespace LearnAgent.Models;

/// <summary>
/// 队友状态
/// </summary>
public static class TeammateStatus
{
    public const string Working = "working";
    public const string Idle = "idle";
    public const string Shutdown = "shutdown";
}

/// <summary>
/// 队友信息
/// </summary>
public class Teammate
{
    public string Name { get; set; } = "";
    public string Role { get; set; } = "";
    public string Status { get; set; } = TeammateStatus.Working;
    public string? SystemPrompt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastActiveAt { get; set; }
}

/// <summary>
/// 团队配置
/// </summary>
public class TeamConfig
{
    public List<Teammate> Members { get; set; } = new();
    public string Leader { get; set; } = "lead";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 消息类型
/// </summary>
public static class MessageType
{
    public const string Message = "message";
    public const string Broadcast = "broadcast";
    public const string Shutdown = "shutdown_request";
    public const string ShutdownResponse = "shutdown_response";
    public const string PlanApproval = "plan_approval";
    public const string PlanApprovalResponse = "plan_approval_response";
}

/// <summary>
/// 团队消息
/// </summary>
public class TeamMessage
{
    public string Type { get; set; } = MessageType.Message;
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string Content { get; set; } = "";
    public double Timestamp { get; set; }
    public Dictionary<string, object>? Extra { get; set; }
    
    /// <summary>
    /// 用于 shutdown_response 和 plan_approval_response
    /// </summary>
    public bool? Approve { get; set; }
    
    /// <summary>
    /// 请求ID，用于响应匹配
    /// </summary>
    public string? RequestId { get; set; }
}
