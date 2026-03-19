using System.Text.Json;
using LearnAgent.Models;
using LearnAgent.Services;

namespace LearnAgent.Tools;

/// <summary>
/// Todo 工具 - 任务规划与进度跟踪
/// </summary>
public class TodoTool : ITool
{
    public string Name => "todo";
    
    public string Description => 
        "Manage task list for multi-step tasks. " +
        "Use this to plan and track progress. " +
        "Parameters: items (array) - list of tasks with id, text, and status (pending/in_progress/completed). " +
        "Only one task can be in_progress at a time. Max 20 tasks.";
    
    private readonly TodoManager todoManager;
    
    public TodoTool(TodoManager todoManager)
    {
        this.todoManager = todoManager;
    }
    
    public Task<string> ExecuteAsync(string argumentsJson)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            // 首先解析为 JsonElement 以处理各种格式
            using var doc = JsonDocument.Parse(argumentsJson);
            var root = doc.RootElement;
            
            if (!root.TryGetProperty("items", out var itemsElement))
            {
                todoManager.Clear();
                return Task.FromResult("Todos cleared.");
            }
            
            List<TodoItem>? items = null;
            
            // 情况1: items 是字符串（模型把 JSON 数组转成了字符串）
            if (itemsElement.ValueKind == JsonValueKind.String)
            {
                var itemsString = itemsElement.GetString() ?? "";
                items = JsonSerializer.Deserialize<List<TodoItem>>(itemsString, options);
            }
            // 情况2: items 是数组（标准格式）
            else if (itemsElement.ValueKind == JsonValueKind.Array)
            {
                items = JsonSerializer.Deserialize<List<TodoItem>>(itemsElement.GetRawText(), options);
            }
            
            if (items == null || items.Count == 0)
            {
                todoManager.Clear();
                return Task.FromResult("Todos cleared.");
            }
            
            var (success, result) = todoManager.Update(items);
            return Task.FromResult(result);
        }
        catch (JsonException ex)
        {
            return Task.FromResult($"Error parsing arguments: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Task.FromResult($"Error: {ex.Message}");
        }
    }
}
