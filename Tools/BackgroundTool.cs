using System.Text.Json;
using LearnAgent.Services;

namespace LearnAgent.Tools;

/// <summary>
/// 后台任务工具 - 支持后台运行命令
/// 
/// 功能：
/// - background_run: 后台启动命令，立即返回
/// - background_check: 检查任务状态
/// - background_list: 列出所有后台任务
/// </summary>
public class BackgroundTool : ITool
{
    public string Name => "background_run";
    
    public string Description => 
        "Run a shell command in the background (non-blocking). " +
        "Returns immediately with a task_id. " +
        "Results are injected before the next LLM call. " +
        "Parameters: command (string) - the shell command to run. " +
        "Use background_check(task_id) to check status, background_list() to list all tasks.";
    
    private readonly BackgroundManager backgroundManager;
    
    public BackgroundTool(BackgroundManager backgroundManager)
    {
        this.backgroundManager = backgroundManager;
    }
    
    public Task<string> ExecuteAsync(string argumentsJson)
    {
        try
        {
            var args = JsonSerializer.Deserialize<BackgroundArguments>(argumentsJson);
            
            // 确定操作类型（通过检查哪个参数有值）
            if (!string.IsNullOrEmpty(args?.Command))
            {
                // 运行命令
                return Task.FromResult(backgroundManager.Run(args.Command));
            }
            
            return Task.FromResult("{\"error\": \"No action specified. Provide 'command' to run, 'task_id' to check, or 'list' to list all.\"}");
        }
        catch (Exception ex)
        {
            return Task.FromResult($"{{\"error\": \"{ex.Message}\"}}");
        }
    }
    
    /// <summary>
    /// 后台任务参数
    /// </summary>
    private class BackgroundArguments
    {
        [System.Text.Json.Serialization.JsonPropertyName("command")]
        public string? Command { get; set; }
    }
}

/// <summary>
/// 后台任务状态检查工具
/// </summary>
public class BackgroundCheckTool : ITool
{
    public string Name => "background_check";
    
    public string Description => 
        "Check the status of a background task. " +
        "Parameters: task_id (string) - the task ID returned by background_run.";
    
    private readonly BackgroundManager backgroundManager;
    
    public BackgroundCheckTool(BackgroundManager backgroundManager)
    {
        this.backgroundManager = backgroundManager;
    }
    
    public Task<string> ExecuteAsync(string argumentsJson)
    {
        try
        {
            var args = JsonSerializer.Deserialize<BackgroundCheckArguments>(argumentsJson);
            
            if (!string.IsNullOrEmpty(args?.TaskId))
            {
                return Task.FromResult(backgroundManager.Check(args.TaskId));
            }
            
            return Task.FromResult("{\"error\": \"task_id is required\"}");
        }
        catch (Exception ex)
        {
            return Task.FromResult($"{{\"error\": \"{ex.Message}\"}}");
        }
    }
    
    private class BackgroundCheckArguments
    {
        [System.Text.Json.Serialization.JsonPropertyName("task_id")]
        public string? TaskId { get; set; }
    }
}

/// <summary>
/// 后台任务列表工具
/// </summary>
public class BackgroundListTool : ITool
{
    public string Name => "background_list";
    
    public string Description => 
        "List all background tasks with their status. " +
        "No parameters required.";
    
    private readonly BackgroundManager backgroundManager;
    
    public BackgroundListTool(BackgroundManager backgroundManager)
    {
        this.backgroundManager = backgroundManager;
    }
    
    public Task<string> ExecuteAsync(string argumentsJson)
    {
        return Task.FromResult(backgroundManager.ListAll());
    }
}
