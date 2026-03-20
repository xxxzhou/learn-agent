using System.Text.Json;
using System.Text.Json.Serialization;
using LearnAgent.Services;

namespace LearnAgent.Tools;

/// <summary>
/// S11: 空闲工具 - 让队友进入空闲状态
/// </summary>
public class IdleTool : ITool
{
    public string Name => "idle";
    
    public string Description => 
        "Enter idle state when you have no immediate work. " +
        "While idle, you will automatically poll for new messages and tasks. " +
        "After 60 seconds of idle with no work, you will auto-shutdown. " +
        "No parameters required.";
    
    private readonly TeammateManager teammateManager;
    
    public IdleTool(TeammateManager teammateManager)
    {
        this.teammateManager = teammateManager;
    }
    
    public Task<string> ExecuteAsync(string argumentsJson)
    {
        // 这个工具主要由队友在 TeammateManager 中处理
        // 这里返回一个提示信息
        return Task.FromResult("Entered idle state. Will poll for new tasks and messages.");
    }
}

/// <summary>
/// S11: 认领任务工具 - 手动认领指定任务
/// </summary>
public class ClaimTaskTool : ITool
{
    public string Name => "claim_task";
    
    public string Description => 
        "Claim a specific task from the task board by its ID. " +
        "Parameters: task_id (integer) - the ID of the task to claim.";
    
    private readonly TeammateManager teammateManager;
    private readonly string? teammateName;
    
    public ClaimTaskTool(TeammateManager teammateManager, string? teammateName = null)
    {
        this.teammateManager = teammateManager;
        this.teammateName = teammateName;
    }
    
    public Task<string> ExecuteAsync(string argumentsJson)
    {
        try
        {
            var args = JsonSerializer.Deserialize<ClaimTaskArguments>(argumentsJson);
            if (args == null || args.TaskId <= 0)
            {
                return Task.FromResult("Error: 'task_id' is required and must be positive");
            }
            
            // 如果没有指定队友名称，使用默认值
            var name = teammateName ?? "unknown";
            return Task.FromResult(teammateManager.ClaimTask(name, args.TaskId));
        }
        catch (Exception ex)
        {
            return Task.FromResult($"Error: {ex.Message}");
        }
    }
    
    private class ClaimTaskArguments
    {
        [JsonPropertyName("task_id")]
        public int TaskId { get; set; }
    }
}

/// <summary>
/// S11: 扫描未认领任务工具 - 查看可认领的任务列表
/// </summary>
public class ScanTasksTool : ITool
{
    public string Name => "scan_tasks";
    
    public string Description => 
        "Scan for unclaimed tasks on the task board. " +
        "Returns a list of tasks that are pending, have no owner, and are not blocked. " +
        "No parameters required.";
    
    private readonly TaskManager taskManager;
    
    public ScanTasksTool(TaskManager taskManager)
    {
        this.taskManager = taskManager;
    }
    
    public Task<string> ExecuteAsync(string argumentsJson)
    {
        try
        {
            var readyTasks = taskManager.GetReadyTasks();
            
            if (readyTasks.Count == 0)
            {
                return Task.FromResult("No unclaimed tasks available.");
            }
            
            var lines = new List<string>();
            lines.Add($"Found {readyTasks.Count} unclaimed task(s):");
            
            foreach (var task in readyTasks)
            {
                lines.Add($"  #{task.Id}: {task.Subject}");
                if (!string.IsNullOrEmpty(task.Description))
                {
                    lines.Add($"      {task.Description[..Math.Min(80, task.Description.Length)]}");
                }
            }
            
            return Task.FromResult(string.Join("\n", lines));
        }
        catch (Exception ex)
        {
            return Task.FromResult($"Error: {ex.Message}");
        }
    }
}
