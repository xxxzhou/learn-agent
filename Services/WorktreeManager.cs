using System.Diagnostics;
using System.Text.Json;
using LearnAgent.Models;

namespace LearnAgent.Services;

/// <summary>
/// S12: Worktree 管理器 - 任务隔离的 git worktree 系统
/// 
/// 功能：
/// - 创建/删除 git worktree
/// - 绑定任务与 worktree
/// - 管理生命周期事件
/// - 支持崩溃恢复
/// </summary>
public class WorktreeManager
{
    private readonly string WorktreesDirectory;
    private readonly string IndexFilePath;
    private readonly string EventsFilePath;
    private readonly string RepoRoot;
    private readonly TaskManager TaskManager;
    
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public WorktreeManager(string workDirectory, TaskManager taskManager)
    {
        WorktreesDirectory = Path.Combine(workDirectory, ".worktrees");
        Directory.CreateDirectory(WorktreesDirectory);
        
        IndexFilePath = Path.Combine(WorktreesDirectory, "index.json");
        EventsFilePath = Path.Combine(WorktreesDirectory, "events.jsonl");
        
        // 找到 git 仓库根目录
        RepoRoot = FindGitRoot(workDirectory);
        
        TaskManager = taskManager;
        
        // 确保索引文件存在
        if (!File.Exists(IndexFilePath))
        {
            File.WriteAllText(IndexFilePath, "[]");
        }
    }

    /// <summary>
    /// 查找 git 仓库根目录
    /// </summary>
    private string FindGitRoot(string startPath)
    {
        var current = startPath;
        while (current != null)
        {
            var gitDir = Path.Combine(current, ".git");
            if (Directory.Exists(gitDir) || File.Exists(gitDir))
            {
                return current;
            }
            current = Directory.GetParent(current)?.FullName;
        }
        return startPath; // 如果没找到，返回原始路径
    }

    /// <summary>
    /// 加载 worktree 索引
    /// </summary>
    private List<WorktreeEntry> LoadIndex()
    {
        if (!File.Exists(IndexFilePath))
        {
            return new List<WorktreeEntry>();
        }
        
        var json = File.ReadAllText(IndexFilePath);
        return JsonSerializer.Deserialize<List<WorktreeEntry>>(json, JsonOpts) ?? new List<WorktreeEntry>();
    }

    /// <summary>
    /// 保存 worktree 索引
    /// </summary>
    private void SaveIndex(List<WorktreeEntry> index)
    {
        var json = JsonSerializer.Serialize(index, JsonOpts);
        File.WriteAllText(IndexFilePath, json);
    }

    /// <summary>
    /// 记录事件
    /// </summary>
    private void EmitEvent(string eventType, WorktreeEntry? worktree = null, TaskItem? task = null)
    {
        var evt = new WorktreeEvent
        {
            Event = eventType,
            Worktree = worktree != null ? new WorktreeEventInfo
            {
                Name = worktree.Name,
                Path = worktree.Path,
                Branch = worktree.Branch,
                Status = worktree.Status
            } : null,
            Task = task != null ? new TaskEventInfo
            {
                Id = task.Id,
                Status = task.Status
            } : null,
            Timestamp = DateTime.UtcNow
        };
        
        var line = JsonSerializer.Serialize(evt, JsonOpts);
        File.AppendAllText(EventsFilePath, line + "\n");
    }

    /// <summary>
    /// 执行 git 命令
    /// </summary>
    private string RunGit(string args, string? workingDir = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = args,
            WorkingDirectory = workingDir ?? RepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        
        using var process = Process.Start(startInfo);
        if (process == null)
        {
            return "Error: Failed to start git process";
        }
        
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        
        if (process.ExitCode != 0)
        {
            return $"Error: {error.Trim()}";
        }
        
        return output.Trim();
    }

    /// <summary>
    /// 创建 worktree
    /// </summary>
    public string Create(string name, int? taskId = null, string? baseBranch = null)
    {
        // 检查名称是否已存在
        var index = LoadIndex();
        if (index.Any(w => w.Name == name))
        {
            return $"Error: Worktree '{name}' already exists";
        }
        
        // 如果绑定了任务，检查任务是否存在
        TaskItem? task = null;
        if (taskId.HasValue)
        {
            var taskJson = TaskManager.Get(taskId.Value);
            if (taskJson.Contains("error"))
            {
                return $"Error: Task {taskId} not found";
            }
            task = JsonSerializer.Deserialize<TaskItem>(taskJson, JsonOpts);
        }
        
        // 触发 before 事件
        EmitEvent("worktree.create.before", new WorktreeEntry { Name = name }, task);
        
        // 创建 worktree 目录
        var wtPath = Path.Combine(WorktreesDirectory, name);
        var branchName = $"wt/{name}";
        
        // 执行 git worktree add
        var baseRef = baseBranch ?? "HEAD";
        var result = RunGit($"worktree add -b {branchName} \"{wtPath}\" {baseRef}");
        
        if (result.StartsWith("Error"))
        {
            EmitEvent("worktree.create.failed", new WorktreeEntry { Name = name }, task);
            return result;
        }
        
        // 创建索引条目
        var entry = new WorktreeEntry
        {
            Name = name,
            Path = wtPath,
            Branch = branchName,
            TaskId = taskId,
            Status = "active",
            CreatedAt = DateTime.UtcNow
        };
        
        index.Add(entry);
        SaveIndex(index);
        
        // 如果绑定了任务，更新任务状态
        if (taskId.HasValue && task != null)
        {
            BindWorktree(taskId.Value, name);
        }
        
        // 触发 after 事件
        EmitEvent("worktree.create.after", entry, task);
        
        return $"Created worktree '{name}' at {wtPath}" + 
               (taskId.HasValue ? $", bound to task #{taskId}" : "");
    }

    /// <summary>
    /// 绑定任务到 worktree
    /// </summary>
    public string BindWorktree(int taskId, string worktreeName)
    {
        var index = LoadIndex();
        var wt = index.FirstOrDefault(w => w.Name == worktreeName);
        if (wt == null)
        {
            return $"Error: Worktree '{worktreeName}' not found";
        }
        
        var taskJson = TaskManager.Get(taskId);
        if (taskJson.Contains("error"))
        {
            return $"Error: Task {taskId} not found";
        }
        
        // 更新 worktree 的 task_id
        wt.TaskId = taskId;
        SaveIndex(index);
        
        // 更新任务的 worktree 字段和状态
        TaskManager.BindWorktree(taskId, worktreeName);
        
        return $"Bound task #{taskId} to worktree '{worktreeName}'";
    }

    /// <summary>
    /// 解绑任务的 worktree
    /// </summary>
    public string UnbindWorktree(int taskId)
    {
        var index = LoadIndex();
        var wt = index.FirstOrDefault(w => w.TaskId == taskId);
        if (wt != null)
        {
            wt.TaskId = null;
            SaveIndex(index);
        }
        
        TaskManager.UnbindWorktree(taskId);
        
        return $"Unbound worktree from task #{taskId}";
    }

    /// <summary>
    /// 在 worktree 中执行命令
    /// </summary>
    public string ExecuteInWorktree(string name, string command)
    {
        var index = LoadIndex();
        var wt = index.FirstOrDefault(w => w.Name == name);
        if (wt == null)
        {
            return $"Error: Worktree '{name}' not found";
        }
        
        if (!Directory.Exists(wt.Path))
        {
            return $"Error: Worktree directory does not exist: {wt.Path}";
        }
        
        var startInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c {command}",
            WorkingDirectory = wt.Path,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        
        using var process = Process.Start(startInfo);
        if (process == null)
        {
            return "Error: Failed to start process";
        }
        
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        
        return string.IsNullOrEmpty(error) ? output : $"{output}\n{error}";
    }

    /// <summary>
    /// 保留 worktree（不删除）
    /// </summary>
    public string Keep(string name)
    {
        var index = LoadIndex();
        var wt = index.FirstOrDefault(w => w.Name == name);
        if (wt == null)
        {
            return $"Error: Worktree '{name}' not found";
        }
        
        wt.Status = "kept";
        SaveIndex(index);
        
        EmitEvent("worktree.keep", wt);
        
        return $"Worktree '{name}' marked as kept";
    }

    /// <summary>
    /// 删除 worktree
    /// </summary>
    public string Remove(string name, bool force = false, bool completeTask = false)
    {
        var index = LoadIndex();
        var wt = index.FirstOrDefault(w => w.Name == name);
        if (wt == null)
        {
            return $"Error: Worktree '{name}' not found";
        }
        
        // 获取关联的任务
        TaskItem? task = null;
        if (wt.TaskId.HasValue)
        {
            var taskJson = TaskManager.Get(wt.TaskId.Value);
            if (!taskJson.Contains("error"))
            {
                task = JsonSerializer.Deserialize<TaskItem>(taskJson, JsonOpts);
            }
        }
        
        // 触发 before 事件
        EmitEvent("worktree.remove.before", wt, task);
        
        // 执行 git worktree remove
        var forceFlag = force ? " --force" : "";
        var result = RunGit($"worktree remove{forceFlag} \"{wt.Path}\"");
        
        if (result.StartsWith("Error"))
        {
            EmitEvent("worktree.remove.failed", wt, task);
            return result;
        }
        
        // 如果需要完成任务
        if (completeTask && wt.TaskId.HasValue)
        {
            TaskManager.Update(wt.TaskId.Value, "completed");
            TaskManager.UnbindWorktree(wt.TaskId.Value);
        }
        
        // 从索引中移除
        index.Remove(wt);
        SaveIndex(index);
        
        // 触发 after 事件
        wt.Status = "removed";
        EmitEvent("worktree.remove.after", wt, task);
        if (completeTask && task != null)
        {
            task.Status = "completed";
            EmitEvent("task.completed", wt, task);
        }
        
        return $"Removed worktree '{name}'" + 
               (completeTask && wt.TaskId.HasValue ? $", completed task #{wt.TaskId}" : "");
    }

    /// <summary>
    /// 列出所有 worktree
    /// </summary>
    public string ListAll()
    {
        var index = LoadIndex();
        
        if (index.Count == 0)
        {
            return "No worktrees.";
        }
        
        var lines = new List<string>();
        lines.Add($"Worktrees ({index.Count}):");
        
        foreach (var wt in index)
        {
            var taskInfo = wt.TaskId.HasValue ? $" [task #{wt.TaskId}]" : "";
            var statusIcon = wt.Status switch
            {
                "active" => "📁",
                "kept" => "📌",
                _ => "❓"
            };
            lines.Add($"  {statusIcon} {wt.Name} ({wt.Branch}){taskInfo}");
            lines.Add($"      Path: {wt.Path}");
        }
        
        return string.Join("\n", lines);
    }

    /// <summary>
    /// 获取 worktree 信息
    /// </summary>
    public WorktreeEntry? Get(string name)
    {
        var index = LoadIndex();
        return index.FirstOrDefault(w => w.Name == name);
    }

    /// <summary>
    /// 获取 worktree 路径
    /// </summary>
    public string? GetPath(string name)
    {
        var wt = Get(name);
        return wt?.Path;
    }

    /// <summary>
    /// 获取任务关联的 worktree
    /// </summary>
    public WorktreeEntry? GetByTaskId(int taskId)
    {
        var index = LoadIndex();
        return index.FirstOrDefault(w => w.TaskId == taskId);
    }

    /// <summary>
    /// 获取事件日志
    /// </summary>
    public string GetEvents(int limit = 20)
    {
        if (!File.Exists(EventsFilePath))
        {
            return "No events recorded.";
        }
        
        var lines = File.ReadAllLines(EventsFilePath)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .TakeLast(limit)
            .ToList();
        
        if (lines.Count == 0)
        {
            return "No events recorded.";
        }
        
        var result = new List<string>();
        result.Add($"Recent events ({lines.Count}):");
        
        foreach (var line in lines)
        {
            try
            {
                var evt = JsonSerializer.Deserialize<WorktreeEvent>(line, JsonOpts);
                if (evt != null)
                {
                    var ts = evt.Timestamp.ToString("HH:mm:ss");
                    var taskInfo = evt.Task != null ? $" task#{evt.Task.Id}" : "";
                    var wtInfo = evt.Worktree != null ? $" [{evt.Worktree.Name}]" : "";
                    result.Add($"  [{ts}] {evt.Event}{wtInfo}{taskInfo}");
                }
            }
            catch
            {
                // 跳过解析失败的行
            }
        }
        
        return string.Join("\n", result);
    }

    /// <summary>
    /// 恢复状态（从磁盘重建）
    /// </summary>
    public string Recover()
    {
        var index = LoadIndex();
        var recovered = 0;
        var cleaned = new List<WorktreeEntry>();
        
        foreach (var wt in index)
        {
            // 检查 worktree 目录是否还存在
            if (Directory.Exists(wt.Path))
            {
                cleaned.Add(wt);
                recovered++;
            }
        }
        
        SaveIndex(cleaned);
        
        return $"Recovered {recovered} worktrees from disk.";
    }
}

/// <summary>
/// Worktree 索引条目
/// </summary>
public class WorktreeEntry
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string Branch { get; set; } = "";
    public int? TaskId { get; set; }
    public string Status { get; set; } = "active";
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Worktree 事件
/// </summary>
public class WorktreeEvent
{
    public string Event { get; set; } = "";
    public WorktreeEventInfo? Worktree { get; set; }
    public TaskEventInfo? Task { get; set; }
    public DateTime Timestamp { get; set; }
}

public class WorktreeEventInfo
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string Branch { get; set; } = "";
    public string Status { get; set; } = "";
}

public class TaskEventInfo
{
    public int Id { get; set; }
    public string Status { get; set; } = "";
}
