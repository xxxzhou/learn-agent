using System.Text.Json;
using System.Text.Json.Serialization;
using LearnAgent.Services;

namespace LearnAgent.Tools;

/// <summary>
/// S12: 创建 Worktree 工具
/// </summary>
public class WorktreeCreateTool : ITool
{
    public string Name => "worktree_create";
    
    public string Description => 
        "Create a new git worktree for isolated task execution. " +
        "Each worktree is an independent working directory with its own branch. " +
        "Parameters: name (string) - unique name for the worktree, " +
        "task_id (integer, optional) - bind to a task (auto-sets status to in_progress), " +
        "base_branch (string, optional) - base branch for the new worktree branch.";
    
    private readonly WorktreeManager worktreeManager;
    
    public WorktreeCreateTool(WorktreeManager worktreeManager)
    {
        this.worktreeManager = worktreeManager;
    }
    
    public Task<string> ExecuteAsync(string argumentsJson)
    {
        try
        {
            var args = JsonSerializer.Deserialize<Arguments>(argumentsJson);
            if (args == null || string.IsNullOrEmpty(args.Name))
            {
                return Task.FromResult("Error: 'name' is required");
            }
            
            return Task.FromResult(worktreeManager.Create(args.Name, args.TaskId, args.BaseBranch));
        }
        catch (Exception ex)
        {
            return Task.FromResult($"Error: {ex.Message}");
        }
    }
    
    private class Arguments
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
        [JsonPropertyName("task_id")]
        public int? TaskId { get; set; }
        [JsonPropertyName("base_branch")]
        public string? BaseBranch { get; set; }
    }
}

/// <summary>
/// S12: 在 Worktree 中执行命令工具
/// </summary>
public class WorktreeExecTool : ITool
{
    public string Name => "worktree_exec";
    
    public string Description => 
        "Execute a shell command in a worktree's isolated directory. " +
        "Parameters: name (string) - worktree name, " +
        "command (string) - command to execute.";
    
    private readonly WorktreeManager worktreeManager;
    
    public WorktreeExecTool(WorktreeManager worktreeManager)
    {
        this.worktreeManager = worktreeManager;
    }
    
    public Task<string> ExecuteAsync(string argumentsJson)
    {
        try
        {
            var args = JsonSerializer.Deserialize<Arguments>(argumentsJson);
            if (args == null || string.IsNullOrEmpty(args.Name))
            {
                return Task.FromResult("Error: 'name' is required");
            }
            if (string.IsNullOrEmpty(args.Command))
            {
                return Task.FromResult("Error: 'command' is required");
            }
            
            return Task.FromResult(worktreeManager.ExecuteInWorktree(args.Name, args.Command));
        }
        catch (Exception ex)
        {
            return Task.FromResult($"Error: {ex.Message}");
        }
    }
    
    private class Arguments
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
        [JsonPropertyName("command")]
        public string? Command { get; set; }
    }
}

/// <summary>
/// S12: 保留 Worktree 工具
/// </summary>
public class WorktreeKeepTool : ITool
{
    public string Name => "worktree_keep";
    
    public string Description => 
        "Mark a worktree as kept (not to be deleted). " +
        "Parameters: name (string) - worktree name.";
    
    private readonly WorktreeManager worktreeManager;
    
    public WorktreeKeepTool(WorktreeManager worktreeManager)
    {
        this.worktreeManager = worktreeManager;
    }
    
    public Task<string> ExecuteAsync(string argumentsJson)
    {
        try
        {
            var args = JsonSerializer.Deserialize<Arguments>(argumentsJson);
            if (args == null || string.IsNullOrEmpty(args.Name))
            {
                return Task.FromResult("Error: 'name' is required");
            }
            
            return Task.FromResult(worktreeManager.Keep(args.Name));
        }
        catch (Exception ex)
        {
            return Task.FromResult($"Error: {ex.Message}");
        }
    }
    
    private class Arguments
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}

/// <summary>
/// S12: 删除 Worktree 工具
/// </summary>
public class WorktreeRemoveTool : ITool
{
    public string Name => "worktree_remove";
    
    public string Description => 
        "Remove a worktree and optionally complete the bound task. " +
        "Parameters: name (string) - worktree name, " +
        "force (boolean, optional) - force removal, " +
        "complete_task (boolean, optional) - mark bound task as completed.";
    
    private readonly WorktreeManager worktreeManager;
    
    public WorktreeRemoveTool(WorktreeManager worktreeManager)
    {
        this.worktreeManager = worktreeManager;
    }
    
    public Task<string> ExecuteAsync(string argumentsJson)
    {
        try
        {
            var args = JsonSerializer.Deserialize<Arguments>(argumentsJson);
            if (args == null || string.IsNullOrEmpty(args.Name))
            {
                return Task.FromResult("Error: 'name' is required");
            }
            
            return Task.FromResult(worktreeManager.Remove(args.Name, args.Force, args.CompleteTask));
        }
        catch (Exception ex)
        {
            return Task.FromResult($"Error: {ex.Message}");
        }
    }
    
    private class Arguments
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
        [JsonPropertyName("force")]
        public bool Force { get; set; }
        [JsonPropertyName("complete_task")]
        public bool CompleteTask { get; set; }
    }
}

/// <summary>
/// S12: 列出 Worktrees 工具
/// </summary>
public class WorktreeListTool : ITool
{
    public string Name => "worktree_list";
    
    public string Description => 
        "List all worktrees with their status and bound tasks. " +
        "No parameters required.";
    
    private readonly WorktreeManager worktreeManager;
    
    public WorktreeListTool(WorktreeManager worktreeManager)
    {
        this.worktreeManager = worktreeManager;
    }
    
    public Task<string> ExecuteAsync(string argumentsJson)
    {
        return Task.FromResult(worktreeManager.ListAll());
    }
}

/// <summary>
/// S12: 绑定任务到 Worktree 工具
/// </summary>
public class WorktreeBindTool : ITool
{
    public string Name => "worktree_bind";
    
    public string Description => 
        "Bind a task to an existing worktree. " +
        "Parameters: task_id (integer) - task ID, " +
        "worktree (string) - worktree name.";
    
    private readonly WorktreeManager worktreeManager;
    
    public WorktreeBindTool(WorktreeManager worktreeManager)
    {
        this.worktreeManager = worktreeManager;
    }
    
    public Task<string> ExecuteAsync(string argumentsJson)
    {
        try
        {
            var args = JsonSerializer.Deserialize<Arguments>(argumentsJson);
            if (args == null || args.TaskId <= 0)
            {
                return Task.FromResult("Error: 'task_id' is required");
            }
            if (string.IsNullOrEmpty(args.Worktree))
            {
                return Task.FromResult("Error: 'worktree' is required");
            }
            
            return Task.FromResult(worktreeManager.BindWorktree(args.TaskId, args.Worktree));
        }
        catch (Exception ex)
        {
            return Task.FromResult($"Error: {ex.Message}");
        }
    }
    
    private class Arguments
    {
        [JsonPropertyName("task_id")]
        public int TaskId { get; set; }
        [JsonPropertyName("worktree")]
        public string? Worktree { get; set; }
    }
}

/// <summary>
/// S12: 查看事件日志工具
/// </summary>
public class WorktreeEventsTool : ITool
{
    public string Name => "worktree_events";
    
    public string Description => 
        "Show recent worktree lifecycle events. " +
        "Parameters: limit (integer, optional) - max events to show (default 20).";
    
    private readonly WorktreeManager worktreeManager;
    
    public WorktreeEventsTool(WorktreeManager worktreeManager)
    {
        this.worktreeManager = worktreeManager;
    }
    
    public Task<string> ExecuteAsync(string argumentsJson)
    {
        try
        {
            var args = JsonSerializer.Deserialize<Arguments>(argumentsJson);
            var limit = args?.Limit ?? 20;
            return Task.FromResult(worktreeManager.GetEvents(limit));
        }
        catch
        {
            return Task.FromResult(worktreeManager.GetEvents(20));
        }
    }
    
    private class Arguments
    {
        [JsonPropertyName("limit")]
        public int Limit { get; set; } = 20;
    }
}
