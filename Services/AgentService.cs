using LearnAgent.Clients;
using LearnAgent.Models;
using LearnAgent.Tools;

namespace LearnAgent.Services;

/// <summary>
/// Agent 服务
/// </summary>
public class AgentService
{
    private readonly ILLMClient client;
    private readonly ToolRegistry toolRegistry;
    private readonly string modelId;
    private readonly List<ChatMessage> messages;
    private readonly string? systemPrompt;
    
    // Nag reminder: 追踪未更新 todo 的轮数
    private int roundsSinceTodo = 0;
    private const int MaxRoundsWithoutTodo = 3;
    
    public AgentService(ILLMClient client, ToolRegistry toolRegistry, string modelId, string? systemPrompt = null)
    {
        this.client = client;
        this.toolRegistry = toolRegistry;
        this.modelId = modelId;
        this.messages = new List<ChatMessage>();
        this.systemPrompt = systemPrompt;
    }
    
    /// <summary>
    /// 发送消息并获取响应
    /// </summary>
    public async Task<string> SendMessageAsync(string userMessage)
    {
        messages.Add(new ChatMessage { Role = "user", Content = userMessage });
        return await ProcessLoopAsync();
    }
    
    /// <summary>
    /// 处理 Agent 循环
    /// </summary>
    private async Task<string> ProcessLoopAsync()
    {
        var finalResponse = "";
        
        while (true)
        {
            // 构建请求消息列表 - 每次请求都重新构建
            var requestMessages = new List<ChatMessage>();
            
            // 系统消息放在第一位
            if (!string.IsNullOrEmpty(systemPrompt))
            {
                requestMessages.Add(new ChatMessage { Role = "system", Content = systemPrompt });
            }
            
            // 添加历史消息（确保没有空内容的消息）
            foreach (var msg in messages)
            {
                // 只添加有内容的消息
                if (msg.Content != null && !string.IsNullOrEmpty(msg.Content.ToString()))
                {
                    requestMessages.Add(msg);
                }
            }
            
            // 确保至少有一条用户消息
            if (!requestMessages.Any(m => m.Role == "user"))
            {
                return "请输入您的问题";
            }
            
            var request = new ChatRequest
            {
                Model = modelId,
                Messages = requestMessages,
                MaxTokens = 2048,
                Tools = toolRegistry.GetToolDefinitions()
            };
            // 提交给大模型
            var response = await client.ChatAsync(request);
            // 大模型返回 
            var choice = response.Choices.FirstOrDefault();
            
            if (choice == null)
            {
                return "No response";
            }
            // 返回的大模型消息
            var assistantMessage = choice.Message;
            
            // 添加助手消息到历史
            messages.Add(new ChatMessage
            {
                Role = "assistant",
                Content = assistantMessage.Content,
                ToolCalls = assistantMessage.ToolCalls
            });
            
            // 检查大模型是否需要调用工具,如果有工具执行,等工具执行完返回再提交
            if (assistantMessage.ToolCalls != null && assistantMessage.ToolCalls.Count > 0)
            {
                var usedTodo = false;
                var toolResults = new List<ChatMessage>();
                
                foreach (var toolCall in assistantMessage.ToolCalls)
                {
                    var result = await ExecuteToolCallAsync(toolCall);
                    
                    // 检查是否使用了 todo 工具
                    if (toolCall.Function?.Name == "todo")
                    {
                        usedTodo = true;
                    }
                    
                    toolResults.Add(new ChatMessage
                    {
                        Role = "tool",
                        Content = result,
                        ToolCallId = toolCall.Id
                    });
                }
                
                // Nag reminder: 如果连续多轮未更新 todo，注入提醒
                roundsSinceTodo = usedTodo ? 0 : roundsSinceTodo + 1;
                if (roundsSinceTodo >= MaxRoundsWithoutTodo)
                {
                    // 在工具结果前插入提醒
                    messages.Add(new ChatMessage
                    {
                        Role = "user",
                        Content = "<reminder>Update your todos to track progress.</reminder>"
                    });
                    roundsSinceTodo = 0; // 重置计数器
                }
                
                // 添加工具结果到历史
                messages.AddRange(toolResults);
                continue;
            }
            
            // 返回文本响应
            if (assistantMessage.Content != null)
            {
                finalResponse = assistantMessage.Content.ToString() ?? "";
            }
            
            break;
        }
        
        return finalResponse;
    }
    
    /// <summary>
    /// 执行工具调用
    /// </summary>
    private async Task<string> ExecuteToolCallAsync(ToolCall toolCall)
    {
        if (toolCall.Function == null)
        {
            return "Error: No function in tool call";
        }
        
        var toolName = toolCall.Function.Name;
        var arguments = toolCall.Function.Arguments;
        
        ConsoleLogger.Tool(toolName, arguments);
        
        var result = await toolRegistry.ExecuteAsync(toolName, arguments);
        
        ConsoleLogger.ToolResult(result);
        
        return result;
    }
    
    /// <summary>
    /// 清空消息历史
    /// </summary>
    public void ClearHistory()
    {
        messages.Clear();
    }
}
