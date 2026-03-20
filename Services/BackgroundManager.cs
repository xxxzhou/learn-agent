using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace LearnAgent.Services;

/// <summary>
/// 后台任务信息
/// </summary>
public class BackgroundTaskInfo
{
    public string TaskId { get; set; } = "";
    public string Command { get; set; } = "";
    public string Status { get; set; } = "running";  // running, completed, failed
    public string? Output { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
}

/// <summary>
/// 后台任务管理器 - 管理后台执行的命令
/// 
/// 功能：
/// - 后台启动命令（非阻塞）
/// - 任务状态追踪
/// - 完成通知队列
/// - 在下次 LLM 调用前注入通知
/// </summary>
public class BackgroundManager
{
    private readonly ConcurrentDictionary<string, BackgroundTaskInfo> Tasks = new();
    private readonly ConcurrentQueue<BackgroundTaskInfo> NotificationQueue = new();
    private readonly string WorkDirectory;
    private int CommandTimeoutMs = 300000; // 5分钟超时
    
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public BackgroundManager(string workDirectory)
    {
        WorkDirectory = workDirectory;
    }

    /// <summary>
    /// 后台运行命令
    /// </summary>
    /// <param name="command">要执行的命令</param>
    /// <returns>任务ID和状态信息</returns>
    public string Run(string command)
    {
        var taskId = Guid.NewGuid().ToString()[..8];
        var taskInfo = new BackgroundTaskInfo
        {
            TaskId = taskId,
            Command = command,
            Status = "running",
            StartTime = DateTime.UtcNow
        };
        
        Tasks[taskId] = taskInfo;
        
        // 启动后台线程执行命令
        Task.Run(() => ExecuteCommand(taskId, command));
        
        return JsonSerializer.Serialize(new
        {
            task_id = taskId,
            status = "started",
            message = $"Background task {taskId} started"
        }, JsonOpts);
    }

    /// <summary>
    /// 执行命令（后台线程）
    /// </summary>
    private void ExecuteCommand(string taskId, string command)
    {
        var taskInfo = Tasks[taskId];
        
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {command}",
                WorkingDirectory = WorkDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            using var process = Process.Start(psi);
            if (process == null)
            {
                taskInfo.Status = "failed";
                taskInfo.Output = "Error: Failed to start process";
                taskInfo.EndTime = DateTime.UtcNow;
                NotificationQueue.Enqueue(taskInfo);
                return;
            }
            
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            
            // 超时保护
            if (!process.WaitForExit(CommandTimeoutMs))
            {
                try { process.Kill(); } catch { }
                taskInfo.Status = "failed";
                taskInfo.Output = $"Error: Command timeout ({CommandTimeoutMs / 1000}s)";
            }
            else
            {
                taskInfo.Status = process.ExitCode == 0 ? "completed" : "failed";
                taskInfo.Output = TruncateOutput((output + error).Trim());
            }
            
            taskInfo.EndTime = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            taskInfo.Status = "failed";
            taskInfo.Output = $"Error: {ex.Message}";
            taskInfo.EndTime = DateTime.UtcNow;
        }
        
        // 将结果加入通知队列
        NotificationQueue.Enqueue(taskInfo);
    }

    /// <summary>
    /// 截断输出（限制 50000 字符）
    /// </summary>
    private static string TruncateOutput(string output, int maxLength = 50000)
    {
        if (string.IsNullOrEmpty(output) || output.Length <= maxLength)
        {
            return output;
        }
        return output[..maxLength] + "\n... (truncated)";
    }

    /// <summary>
    /// 检查任务状态
    /// </summary>
    /// <param name="taskId">任务ID</param>
    /// <returns>任务状态信息</returns>
    public string Check(string taskId)
    {
        if (!Tasks.TryGetValue(taskId, out var taskInfo))
        {
            return $"{{\"error\": \"Task {taskId} not found\"}}";
        }
        
        return JsonSerializer.Serialize(taskInfo, JsonOpts);
    }

    /// <summary>
    /// 列出所有后台任务
    /// </summary>
    public string ListAll()
    {
        var tasks = Tasks.Values.ToList();
        
        if (tasks.Count == 0)
        {
            return "No background tasks.";
        }
        
        var lines = new List<string>();
        foreach (var task in tasks.OrderBy(t => t.StartTime))
        {
            var duration = task.EndTime.HasValue
                ? $" (took {(task.EndTime.Value - task.StartTime).TotalSeconds:F1}s)"
                : $" (running for {(DateTime.UtcNow - task.StartTime).TotalSeconds:F1}s)";
            
            var statusIcon = task.Status switch
            {
                "running" => "⏳",
                "completed" => "✅",
                "failed" => "❌",
                _ => "❓"
            };
            
            lines.Add($"{statusIcon} [{task.TaskId}] {task.Status}{duration}");
            lines.Add($"   Command: {task.Command}");
            if (!string.IsNullOrEmpty(task.Output) && task.Status != "running")
            {
                var preview = task.Output.Length > 100 
                    ? task.Output[..100] + "..." 
                    : task.Output;
                lines.Add($"   Output: {preview}");
            }
        }
        
        return string.Join("\n", lines);
    }

    /// <summary>
    /// 排空通知队列（在每次 LLM 调用前调用）
    /// </summary>
    /// <returns>需要注入的通知消息列表</returns>
    public List<BackgroundTaskInfo> DrainNotifications()
    {
        var notifications = new List<BackgroundTaskInfo>();
        
        while (NotificationQueue.TryDequeue(out var taskInfo))
        {
            notifications.Add(taskInfo);
        }
        
        return notifications;
    }

    /// <summary>
    /// 获取正在运行的任务数量
    /// </summary>
    public int GetRunningCount()
    {
        return Tasks.Values.Count(t => t.Status == "running");
    }

    /// <summary>
    /// 清理已完成的任务（可选）
    /// </summary>
    public void ClearCompleted()
    {
        var toRemove = Tasks.Values
            .Where(t => t.Status != "running")
            .Select(t => t.TaskId)
            .ToList();
        
        foreach (var taskId in toRemove)
        {
            Tasks.TryRemove(taskId, out _);
        }
    }
}
