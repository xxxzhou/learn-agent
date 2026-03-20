using System.Text.Json;
using System.Text.Json.Serialization;
using LearnAgent.Services;

namespace LearnAgent.Tools;

/// <summary>
/// 文件任务工具 - 持久化任务系统，支持依赖关系
/// 
/// 工具名称: file_task
/// 参数:
///   - action: 操作类型 (create, get, update, list, claim, unclaim, delete)
///   - subject: 任务主题 (create时使用)
///   - description: 任务描述 (create时使用)
///   - task_id: 任务ID (get, update, claim, unclaim, delete时使用)
///   - status: 任务状态 (update时使用: pending, in_progress, completed)
///   - add_blocked_by: 添加依赖的任务ID列表 (update时使用)
///   - add_blocks: 添加阻塞的任务ID列表 (update时使用)
///   - owner: 认领者 (claim时使用)
/// </summary>
public class FileTaskTool : ITool
{
    public string Name => "file_task";
    
    public string Description => 
        "Manage persistent file-based tasks with dependencies. " +
        "Tasks survive context compression and persist across sessions. " +
        "Use for multi-step projects that span multiple conversations. " +
        "Parameters: action (create/get/update/list/claim/unclaim/delete), " +
        "task_id, subject, description, status, owner, add_blocked_by, add_blocks.";
    
    private readonly TaskManager taskManager;
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    
    public FileTaskTool(TaskManager taskManager)
    {
        this.taskManager = taskManager;
    }
    
    public async Task<string> ExecuteAsync(string argumentsJson)
    {
        try
        {
            var args = JsonSerializer.Deserialize<FileTaskArguments>(argumentsJson, JsonOptions);
            
            if (args == null || string.IsNullOrEmpty(args.Action))
            {
                return "Error: action is required. Valid actions: create, get, update, list, claim, unclaim, delete";
            }
            
            var action = args.Action.ToLowerInvariant();
            
            return action switch
            {
                "create" => await TaskCreateAsync(args),
                "get" => await TaskGetAsync(args),
                "update" => await TaskUpdateAsync(args),
                "list" => await TaskListAsync(args),
                "claim" => await TaskClaimAsync(args),
                "unclaim" => await TaskUnclaimAsync(args),
                "delete" => await TaskDeleteAsync(args),
                _ => $"Error: Unknown action '{action}'. Valid: create, get, update, list, claim, unclaim, delete"
            };
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
    
    private Task<string> TaskCreateAsync(FileTaskArguments args)
    {
        if (string.IsNullOrEmpty(args.Subject))
        {
            return Task.FromResult("Error: subject is required for create action");
        }
        
        var result = taskManager.Create(args.Subject, args.Description ?? "");
        return Task.FromResult(result);
    }
    
    private Task<string> TaskGetAsync(FileTaskArguments args)
    {
        if (args.TaskId == null || args.TaskId <= 0)
        {
            return Task.FromResult("Error: task_id is required for get action");
        }
        
        var result = taskManager.Get(args.TaskId.Value);
        return Task.FromResult(result);
    }
    
    private Task<string> TaskUpdateAsync(FileTaskArguments args)
    {
        if (args.TaskId == null || args.TaskId <= 0)
        {
            return Task.FromResult("Error: task_id is required for update action");
        }
        
        var result = taskManager.Update(
            args.TaskId.Value, 
            args.Status, 
            args.AddBlockedBy, 
            args.AddBlocks);
        
        return Task.FromResult(result);
    }
    
    private Task<string> TaskListAsync(FileTaskArguments args)
    {
        var result = taskManager.ListAll();
        return Task.FromResult(result);
    }
    
    private Task<string> TaskClaimAsync(FileTaskArguments args)
    {
        if (args.TaskId == null || args.TaskId <= 0)
        {
            return Task.FromResult("Error: task_id is required for claim action");
        }
        
        if (string.IsNullOrEmpty(args.Owner))
        {
            return Task.FromResult("Error: owner is required for claim action");
        }
        
        var result = taskManager.Claim(args.TaskId.Value, args.Owner);
        return Task.FromResult(result);
    }
    
    private Task<string> TaskUnclaimAsync(FileTaskArguments args)
    {
        if (args.TaskId == null || args.TaskId <= 0)
        {
            return Task.FromResult("Error: task_id is required for unclaim action");
        }
        
        var result = taskManager.Unclaim(args.TaskId.Value);
        return Task.FromResult(result);
    }
    
    private Task<string> TaskDeleteAsync(FileTaskArguments args)
    {
        if (args.TaskId == null || args.TaskId <= 0)
        {
            return Task.FromResult("Error: task_id is required for delete action");
        }
        
        var result = taskManager.Delete(args.TaskId.Value);
        return Task.FromResult(result);
    }
    
    private class FileTaskArguments
    {
        [JsonPropertyName("action")]
        public string? Action { get; set; }
        
        [JsonPropertyName("task_id")]
        public int? TaskId { get; set; }
        
        [JsonPropertyName("subject")]
        public string? Subject { get; set; }
        
        [JsonPropertyName("description")]
        public string? Description { get; set; }
        
        [JsonPropertyName("status")]
        public string? Status { get; set; }
        
        [JsonPropertyName("owner")]
        public string? Owner { get; set; }
        
        [JsonPropertyName("add_blocked_by")]
        public List<int>? AddBlockedBy { get; set; }
        
        [JsonPropertyName("add_blocks")]
        public List<int>? AddBlocks { get; set; }
    }
}
