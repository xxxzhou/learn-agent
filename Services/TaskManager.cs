using System.Text.Json;
using LearnAgent.Models;

namespace LearnAgent.Services;

/// <summary>
/// 任务管理器 - 持久化文件任务系统，支持依赖关系
/// 
/// 功能：
/// - 任务持久化到 .tasks/ 目录（JSON文件）
/// - 依赖管理（blockedBy/blocks）
/// - 自动解锁：完成任务后自动移除依赖
/// - 任务认领（claim/unclaim）
/// </summary>
public class TaskManager
{
    private readonly string TasksDirectory;
    private int NextId = 1;
    
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public TaskManager(string workDirectory)
    {
        TasksDirectory = Path.Combine(workDirectory, ".tasks");
        Directory.CreateDirectory(TasksDirectory);
        NextId = GetMaxId() + 1;
    }

    /// <summary>
    /// 获取最大任务ID
    /// </summary>
    private int GetMaxId()
    {
        var maxId = 0;
        foreach (var file in Directory.GetFiles(TasksDirectory, "task_*.json"))
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            var parts = fileName.Split('_');
            if (parts.Length >= 2 && int.TryParse(parts[1], out var id))
            {
                if (id > maxId) maxId = id;
            }
        }
        return maxId;
    }

    /// <summary>
    /// 加载任务
    /// </summary>
    private TaskItem? LoadTask(int taskId)
    {
        var path = Path.Combine(TasksDirectory, $"task_{taskId}.json");
        if (!File.Exists(path)) return null;
        
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<TaskItem>(json, JsonOpts);
    }

    /// <summary>
    /// 保存任务
    /// </summary>
    private void SaveTask(TaskItem task)
    {
        task.UpdatedAt = DateTime.UtcNow;
        var path = Path.Combine(TasksDirectory, $"task_{task.Id}.json");
        var json = JsonSerializer.Serialize(task, JsonOpts);
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// 创建新任务
    /// </summary>
    /// <param name="subject">任务主题</param>
    /// <param name="description">任务描述</param>
    /// <returns>JSON格式的任务信息</returns>
    public string Create(string subject, string description = "")
    {
        var task = new TaskItem
        {
            Id = NextId++,
            Subject = subject,
            Description = description,
            Status = FileTaskStatus.Pending,
            BlockedBy = [],
            Blocks = []
        };
        
        SaveTask(task);
        return JsonSerializer.Serialize(task, JsonOpts);
    }

    /// <summary>
    /// 获取任务详情
    /// </summary>
    /// <param name="taskId">任务ID</param>
    /// <returns>JSON格式的任务信息</returns>
    public string Get(int taskId)
    {
        var task = LoadTask(taskId);
        if (task == null)
        {
            return $"{{\"error\": \"Task {taskId} not found\"}}";
        }
        return JsonSerializer.Serialize(task, JsonOpts);
    }

    /// <summary>
    /// 更新任务状态或依赖
    /// </summary>
    /// <param name="taskId">任务ID</param>
    /// <param name="status">新状态 (pending/in_progress/completed)</param>
    /// <param name="addBlockedBy">添加依赖的任务ID</param>
    /// <param name="addBlocks">添加阻塞的任务ID</param>
    /// <returns>JSON格式的任务信息</returns>
    public string Update(int taskId, string? status = null, List<int>? addBlockedBy = null, List<int>? addBlocks = null)
    {
        var task = LoadTask(taskId);
        if (task == null)
        {
            return $"{{\"error\": \"Task {taskId} not found\"}}";
        }

        if (!string.IsNullOrEmpty(status))
        {
            if (!FileTaskStatus.IsValid(status))
            {
                return $"{{\"error\": \"Invalid status '{status}'. Valid: pending, in_progress, completed\"}}";
            }
            
            task.Status = status;
            
            // 完成任务时清除所有依赖
            if (status == FileTaskStatus.Completed)
            {
                ClearDependency(taskId);
            }
        }

        // 添加被阻塞的任务
        if (addBlockedBy != null && addBlockedBy.Count > 0)
        {
            foreach (var depId in addBlockedBy)
            {
                if (!task.BlockedBy.Contains(depId))
                {
                    task.BlockedBy.Add(depId);
                }
            }
        }

        // 添加阻塞的任务（双向关联）
        if (addBlocks != null && addBlocks.Count > 0)
        {
            foreach (var blockedId in addBlocks)
            {
                if (!task.Blocks.Contains(blockedId))
                {
                    task.Blocks.Add(blockedId);
                }
                
                // 双向更新：blocked任务也要添加当前任务到其blockedBy
                var blockedTask = LoadTask(blockedId);
                if (blockedTask != null && !blockedTask.BlockedBy.Contains(taskId))
                {
                    blockedTask.BlockedBy.Add(taskId);
                    SaveTask(blockedTask);
                }
            }
        }

        SaveTask(task);
        return JsonSerializer.Serialize(task, JsonOpts);
    }

    /// <summary>
    /// 清除任务依赖 - 从所有任务的blockedBy中移除指定任务ID
    /// </summary>
    private void ClearDependency(int completedId)
    {
        foreach (var file in Directory.GetFiles(TasksDirectory, "task_*.json"))
        {
            var json = File.ReadAllText(file);
            var task = JsonSerializer.Deserialize<TaskItem>(json, JsonOpts);
            if (task != null && task.BlockedBy.Contains(completedId))
            {
                task.BlockedBy.Remove(completedId);
                SaveTask(task);
            }
        }
    }

    /// <summary>
    /// 列出所有任务
    /// </summary>
    /// <returns>格式化任务列表</returns>
    public string ListAll()
    {
        var tasks = new List<TaskItem>();
        
        foreach (var file in Directory.GetFiles(TasksDirectory, "task_*.json"))
        {
            var json = File.ReadAllText(file);
            var task = JsonSerializer.Deserialize<TaskItem>(json, JsonOpts);
            if (task != null)
            {
                tasks.Add(task);
            }
        }

        if (tasks.Count == 0)
        {
            return "No tasks.";
        }

        tasks.Sort((a, b) => a.Id.CompareTo(b.Id));

        var lines = new List<string>();
        foreach (var task in tasks)
        {
            var marker = task.Status switch
            {
                FileTaskStatus.Pending => "[ ]",
                FileTaskStatus.InProgress => "[>]",
                FileTaskStatus.Completed => "[x]",
                _ => "[?]"
            };
            
            var owner = string.IsNullOrEmpty(task.Owner) ? "" : $" @{task.Owner}";
            var blocked = task.BlockedBy.Count > 0 ? $" (blocked by: [{string.Join(", ", task.BlockedBy)}])" : "";
            
            lines.Add($"{marker} #{task.Id}: {task.Subject}{owner}{blocked}");
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// 认领任务
    /// </summary>
    /// <param name="taskId">任务ID</param>
    /// <param name="owner">认领者</param>
    /// <returns>操作结果</returns>
    public string Claim(int taskId, string owner)
    {
        var task = LoadTask(taskId);
        if (task == null)
        {
            return $"Error: Task {taskId} not found";
        }
        
        if (!string.IsNullOrEmpty(task.Owner) && task.Owner != owner)
        {
            return $"Error: Task {taskId} is already claimed by {task.Owner}";
        }
        
        task.Owner = owner;
        task.Status = FileTaskStatus.InProgress;
        SaveTask(task);
        
        return $"Claimed task #{taskId} for {owner}";
    }

    /// <summary>
    /// 释放认领
    /// </summary>
    /// <param name="taskId">任务ID</param>
    /// <returns>操作结果</returns>
    public string Unclaim(int taskId)
    {
        var task = LoadTask(taskId);
        if (task == null)
        {
            return $"Error: Task {taskId} not found";
        }
        
        task.Owner = "";
        task.Status = FileTaskStatus.Pending;
        SaveTask(task);
        
        return $"Released task #{taskId}";
    }

    /// <summary>
    /// 获取可执行的任务（pending状态，无owner，无blockedBy）
    /// </summary>
    /// <returns>可执行任务列表</returns>
    public List<TaskItem> GetReadyTasks()
    {
        var readyTasks = new List<TaskItem>();
        
        foreach (var file in Directory.GetFiles(TasksDirectory, "task_*.json"))
        {
            var json = File.ReadAllText(file);
            var task = JsonSerializer.Deserialize<TaskItem>(json, JsonOpts);
            if (task != null && 
                task.Status == FileTaskStatus.Pending && 
                string.IsNullOrEmpty(task.Owner) && 
                task.BlockedBy.Count == 0)
            {
                readyTasks.Add(task);
            }
        }

        return readyTasks;
    }

    /// <summary>
    /// 删除任务
    /// </summary>
    /// <param name="taskId">任务ID</param>
    /// <returns>操作结果</returns>
    public string Delete(int taskId)
    {
        var path = Path.Combine(TasksDirectory, $"task_{taskId}.json");
        if (!File.Exists(path))
        {
            return $"Error: Task {taskId} not found";
        }
        
        File.Delete(path);
        return $"Task {taskId} deleted";
    }

    /// <summary>
    /// 统计任务数量
    /// </summary>
    public (int pending, int inProgress, int completed) GetStats()
    {
        var pending = 0;
        var inProgress = 0;
        var completed = 0;
        
        foreach (var file in Directory.GetFiles(TasksDirectory, "task_*.json"))
        {
            var json = File.ReadAllText(file);
            var task = JsonSerializer.Deserialize<TaskItem>(json, JsonOpts);
            if (task != null)
            {
                switch (task.Status)
                {
                    case FileTaskStatus.Pending:
                        pending++;
                        break;
                    case FileTaskStatus.InProgress:
                        inProgress++;
                        break;
                    case FileTaskStatus.Completed:
                        completed++;
                        break;
                }
            }
        }
        
        return (pending, inProgress, completed);
    }
}
