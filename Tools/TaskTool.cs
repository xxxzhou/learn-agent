using System.Text.Json;
using LearnAgent.Services;

namespace LearnAgent.Tools;

/// <summary>
/// Task 工具 - 派遣子代理执行任务
/// </summary>
public class TaskTool : ITool
{
    public string Name => "task";
    
    public string Description => 
        "Spawn a subagent with fresh context to handle a subtask. " +
        "Use for exploration, search, or complex operations that need isolation. " +
        "Parameters: prompt (string) - the task description, " +
        "description (optional string) - short summary for logging. " +
        "The subagent shares the filesystem but has its own conversation context.";
    
    private readonly SubagentService subagentService;
    
    public TaskTool(SubagentService subagentService)
    {
        this.subagentService = subagentService;
    }
    
    public async Task<string> ExecuteAsync(string argumentsJson)
    {
        try
        {
            var args = JsonSerializer.Deserialize<TaskArguments>(argumentsJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            if (args == null || string.IsNullOrEmpty(args.Prompt))
            {
                return "Error: prompt is required";
            }
            
            var task = new Models.SubagentTask
            {
                Prompt = args.Prompt,
                Description = args.Description,
                MaxRounds = args.MaxRounds ?? 30
            };
            
            // 显示任务开始
            var desc = args.Description ?? "subtask";
            ConsoleLogger.Info($"Starting subagent task: {desc}");
            ConsoleLogger.Separator('-');
            
            // 执行子代理任务
            var result = await subagentService.ExecuteAsync(task);
            
            // 显示任务完成
            ConsoleLogger.Separator('-');
            ConsoleLogger.Info($"Subagent completed in {result.RoundsExecuted} rounds");
            
            if (!result.Success)
            {
                return $"Subagent failed: {result.Error}";
            }
            
            return result.Summary;
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
    
    private class TaskArguments
    {
        public string? Prompt { get; set; }
        public string? Description { get; set; }
        public int? MaxRounds { get; set; }
    }
}
