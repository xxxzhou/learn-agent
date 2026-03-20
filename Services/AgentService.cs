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
    private ContextCompressor? compressor;
    
    // Nag reminder: 追踪未更新 todo 的轮数
    private int roundsSinceTodo = 0;
    private const int MaxRoundsWithoutTodo = 3;
    
    // 手动压缩标志
    private bool pendingCompact = false;

    // 上一次压缩的摘要内容
    private string? lastSummary = null;
    
    public AgentService(ILLMClient client, ToolRegistry toolRegistry, string modelId, string? systemPrompt = null)
    {
        this.client = client;
        this.toolRegistry = toolRegistry;
        this.modelId = modelId;
        this.messages = new List<ChatMessage>();
        this.systemPrompt = systemPrompt;
    }
    
    /// <summary>
    /// 设置上下文压缩器
    /// </summary>
    public void SetCompressor(ContextCompressor compressor)
    {
        this.compressor = compressor;
    }
    
    /// <summary>
    /// 发送消息并获取响应
    /// </summary>
    public async Task<string> SendMessageAsync(string userMessage)
    {
        messages.Add(new ChatMessage { Role = "user", Content = userMessage });
        var response = await ProcessLoopAsync();

        // 如果有摘要，打印特殊格式
        if (!string.IsNullOrEmpty(lastSummary))
        {
            PrintSummary(lastSummary);
            lastSummary = null;  // 重置
        }

        return response;
    }

    /// <summary>
    /// 打印压缩摘要（带特殊颜色）
    /// </summary>
    private void PrintSummary(string summary)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine("                    【上下文压缩摘要】");
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(summary);
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.ResetColor();
        Console.WriteLine();
    }
    
    /// <summary>
    /// 处理 Agent 循环
    /// </summary>
    private async Task<string> ProcessLoopAsync()
    {
        var finalResponse = "";
        
        while (true)
        {
            // Layer 1: micro_compact - 每次调用前静默执行
            if (compressor != null)
            {
                var compactedMessages = compressor.MicroCompact(messages);
                messages.Clear();
                messages.AddRange(compactedMessages);
                
                // Layer 2: auto_compact - 检查是否超过 token 阈值
                if (compressor.NeedsAutoCompact(messages))
                {
                    ConsoleLogger.Info("Token threshold exceeded, auto-compacting...");
                    var compressedMessages = await compressor.AutoCompactAsync(messages);
                    messages.Clear();
                    messages.AddRange(compressedMessages);
                }
                
                // Layer 3: 检查是否需要手动压缩
                if (pendingCompact)
                {
                    ConsoleLogger.Info("Manual compact triggered");
                    var result = await compressor.CompactAsync(messages);
                    lastSummary = result;  // 保存摘要
                    messages.Clear();
                    messages.Add(new ChatMessage
                    {
                        Role = "user",
                        Content = $"[Compressed conversation]\n\n{result}"
                    });
                    pendingCompact = false;
                }

                // Layer 2: 检查是否是自动压缩后的首次响应
                if (compressor != null && compressor.WasAutoCompressed())
                {
                    var summary = compressor.GetLastSummary();  // 获取摘要
                    compressor.ResetAutoCompressed();  // 重置状态

                    // 立即打印摘要
                    if (!string.IsNullOrEmpty(summary))
                    {
                        PrintSummary(summary);
                    }
                }
            }
            
            // 构建请求消息列表 - 每次请求都重新构建
            var requestMessages = new List<ChatMessage>();
            
            // 系统消息放在第一位
            if (!string.IsNullOrEmpty(systemPrompt))
            {
                requestMessages.Add(new ChatMessage { Role = "system", Content = systemPrompt });
            }
            
            // 添加历史消息（包括 tool 消息）
            foreach (var msg in messages)
            {
                // tool 消息必须有 ToolCallId，不能被过滤
                if (msg.Role == "tool")
                {
                    requestMessages.Add(msg);
                }
                // 其他消息检查 Content 是否有效
                else if (msg.Content != null && !string.IsNullOrEmpty(msg.Content.ToString()))
                {
                    requestMessages.Add(msg);
                }
                // assistant 消息可能只有 ToolCalls 没有 Content
                else if (msg.Role == "assistant" && msg.ToolCalls != null && msg.ToolCalls.Count > 0)
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
            
            // 调试输出 token 估算
            if (compressor != null)
            {
                var estimatedTokens = compressor.EstimateTokens(requestMessages);
                ConsoleLogger.Debug($"Estimated tokens: {estimatedTokens}");
            }
            
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
                
                // 添加工具结果到历史
                messages.AddRange(toolResults);
                
                // Nag reminder: 如果连续多轮未更新 todo，在工具结果后注入提醒
                roundsSinceTodo = usedTodo ? 0 : roundsSinceTodo + 1;
                if (roundsSinceTodo >= MaxRoundsWithoutTodo)
                {
                    messages.Add(new ChatMessage
                    {
                        Role = "user",
                        Content = "<reminder>Update your todos to track progress.</reminder>"
                    });
                    roundsSinceTodo = 0; // 重置计数器
                }
                
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
        
        // 检查是否是 compact 工具
        if (toolName == "compact")
        {
            if (compressor == null)
            {
                return "Error: Compressor not initialized. Context compression is not available.";
            }
            
            // 设置手动压缩标志
            pendingCompact = true;
            return "Compacting context... This will be applied on the next round.";
        }
        
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
