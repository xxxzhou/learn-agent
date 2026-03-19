using LearnAgent.Clients;
using LearnAgent.Models;
using LearnAgent.Tools;

namespace LearnAgent.Services;

/// <summary>
/// 子代理服务 - 独立上下文执行子任务
/// </summary>
public class SubagentService
{
    private readonly ILLMClient client;
    private readonly ToolRegistry toolRegistry;
    private readonly string modelId;
    private readonly string systemPrompt;
    
    /// <summary>
    /// 子代理默认系统提示
    /// </summary>
    private const string DefaultSystemPrompt = 
        "You are a coding subagent. Complete the given task, then summarize your findings. " +
        "Be concise and focused. Return only the essential results.\n\n" +
        "IMPORTANT: You are on Windows. Use Windows commands:\n" +
        "- Use 'dir' instead of 'ls'\n" +
        "- Use 'findstr' instead of 'grep'\n" +
        "- Use 'type' instead of 'cat'\n" +
        "- Do NOT use 'find', 'grep', 'ls', 'cat', or Linux-specific commands\n" +
        "- Do NOT use shell redirects (>, >>, 2>)";
    
    public SubagentService(
        ILLMClient client, 
        ToolRegistry toolRegistry, 
        string modelId,
        string? workDirectory = null)
    {
        this.client = client;
        this.toolRegistry = toolRegistry;
        this.modelId = modelId;
        this.systemPrompt = workDirectory != null
            ? $"You are a coding subagent at {workDirectory}. Complete the given task, then summarize your findings.\n\n" +
              "IMPORTANT: You are on Windows. Use Windows commands:\n" +
              "- Use 'dir' instead of 'ls'\n" +
              "- Use 'findstr' instead of 'grep'\n" +
              "- Use 'type' instead of 'cat'\n" +
              "- Do NOT use 'find', 'grep', 'ls', 'cat', or Linux-specific commands\n" +
              "- Do NOT use shell redirects (>, >>, 2>)"
            : DefaultSystemPrompt;
    }
    
    /// <summary>
    /// 执行子代理任务
    /// </summary>
    /// <param name="task">任务请求</param>
    /// <returns>执行结果</returns>
    public async Task<SubagentResult> ExecuteAsync(SubagentTask task)
    {
        try
        {
            // 创建全新的消息历史（上下文隔离）
            var messages = new List<ChatMessage>
            {
                new() { Role = "user", Content = task.Prompt }
            };
            
            // 获取子代理工具（排除 task 工具，防止递归）
            var subagentTools = toolRegistry.GetToolDefinitions()
                .Where(t => t.Function?.Name != "task")
                .ToList();
            
            var roundsExecuted = 0;
            string finalResponse = "";
            
            // 执行循环（最多 MaxRounds 轮）
            for (int round = 0; round < task.MaxRounds; round++)
            {
                roundsExecuted = round + 1;
                
                var request = new ChatRequest
                {
                    Model = modelId,
                    Messages = BuildRequestMessages(messages),
                    MaxTokens = 4096,
                    Tools = subagentTools
                };
                
                var response = await client.ChatAsync(request);
                var choice = response.Choices.FirstOrDefault();
                
                if (choice == null)
                {
                    return new SubagentResult
                    {
                        Success = false,
                        Error = "No response from model",
                        RoundsExecuted = roundsExecuted
                    };
                }
                
                var assistantMessage = choice.Message;
                
                // 添加助手消息到历史
                messages.Add(new ChatMessage
                {
                    Role = "assistant",
                    Content = assistantMessage.Content,
                    ToolCalls = assistantMessage.ToolCalls
                });
                
                // 检查是否需要调用工具
                if (assistantMessage.ToolCalls == null || assistantMessage.ToolCalls.Count == 0)
                {
                    // 没有工具调用，任务完成
                    finalResponse = assistantMessage.Content?.ToString() ?? "";
                    break;
                }
                
                // 执行工具调用
                foreach (var toolCall in assistantMessage.ToolCalls)
                {
                    var toolName = toolCall.Function?.Name ?? "";
                    var arguments = toolCall.Function?.Arguments ?? "{}";
                    
                    ConsoleLogger.Tool(toolName, arguments);
                    
                    var result = await toolRegistry.ExecuteAsync(toolName, arguments);
                    
                    ConsoleLogger.ToolResult(result, truncate: true);
                    
                    messages.Add(new ChatMessage
                    {
                        Role = "tool",
                        Content = result,
                        ToolCallId = toolCall.Id
                    });
                }
            }
            
            return new SubagentResult
            {
                Success = true,
                Summary = string.IsNullOrEmpty(finalResponse) 
                    ? "(Task completed but no summary provided)" 
                    : finalResponse,
                RoundsExecuted = roundsExecuted
            };
        }
        catch (Exception ex)
        {
            return new SubagentResult
            {
                Success = false,
                Error = ex.Message,
                RoundsExecuted = 0
            };
        }
    }
    
    /// <summary>
    /// 构建请求消息列表
    /// </summary>
    private List<ChatMessage> BuildRequestMessages(List<ChatMessage> history)
    {
        var messages = new List<ChatMessage>();
        
        // 添加系统提示
        if (!string.IsNullOrEmpty(systemPrompt))
        {
            messages.Add(new ChatMessage { Role = "system", Content = systemPrompt });
        }
        
        // 添加历史消息
        foreach (var msg in history)
        {
            if (msg.Content != null && !string.IsNullOrEmpty(msg.Content.ToString()))
            {
                messages.Add(msg);
            }
        }
        
        return messages;
    }
}
