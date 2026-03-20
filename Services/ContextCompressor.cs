using System.Text.Json;
using System.Text.RegularExpressions;
using LearnAgent.Clients;
using LearnAgent.Models;

namespace LearnAgent.Services;

/// <summary>
/// 上下文压缩器 - 三层压缩策略
/// </summary>
public class ContextCompressor
{
    private readonly ILLMClient client;
    private readonly string workDirectory;
    private readonly string transcriptDirectory;
    
    // 配置
    private const int KEEP_RECENT = 3;  // 保留最近 N 个 tool_result 完整
    private const int TOKEN_THRESHOLD = 5000;  // token 阈值
    private const int TOOL_RESULT_LENGTH_THRESHOLD = 100;  // tool_result 长度阈值
    private readonly string summaryModel;  // 摘要使用的模型
    
    public ContextCompressor(ILLMClient client, string workDirectory, string modelId)
    {
        this.client = client;
        this.workDirectory = workDirectory;
        this.summaryModel = modelId;
        this.transcriptDirectory = Path.Combine(workDirectory, ".transcripts");
        
        // 确保 transcript 目录存在
        if (!Directory.Exists(transcriptDirectory))
        {
            Directory.CreateDirectory(transcriptDirectory);
        }
    }
    
    /// <summary>
    /// Layer 1: micro_compact - 每次调用前静默执行
    /// 将旧的 tool_result 替换为占位符
    /// </summary>
    public List<ChatMessage> MicroCompact(List<ChatMessage> messages)
    {
        var result = new List<ChatMessage>(messages);
        
        // 收集所有 tool_result
        var toolResults = new List<(int index, ChatMessage msg)>();
        
        for (int i = 0; i < result.Count; i++)
        {
            var msg = result[i];
            if (msg.Role == "tool" && !string.IsNullOrEmpty(msg.Content?.ToString()) && msg.Content?.ToString()?.Length > TOOL_RESULT_LENGTH_THRESHOLD)
            {
                toolResults.Add((i, msg));
            }
        }
        
        // 如果 tool_result 数量超过保留阈值，替换旧的为占位符
        if (toolResults.Count > KEEP_RECENT)
        {
            var toCompact = toolResults.Take(toolResults.Count - KEEP_RECENT);
            foreach (var (index, msg) in toCompact)
            {
                // 从消息中提取工具名称
                var toolName = ExtractToolName(msg.ToolCallId);
                var content = msg.Content?.ToString() ?? "";
                var summary = $"[Previous: used {toolName}, result length: {content.Length} chars]";
                
                result[index] = new ChatMessage
                {
                    Role = "tool",
                    Content = summary,
                    ToolCallId = msg.ToolCallId
                };
                
                ConsoleLogger.Debug($"Micro-compacted tool result: {toolName}");
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// 提取工具名称
    /// </summary>
    private string ExtractToolName(string? toolCallId)
    {
        // 从 toolCallId 中提取工具名
        // 格式通常是 "tool_call_id" 或类似
        if (string.IsNullOrEmpty(toolCallId))
            return "unknown";
        
        // 如果是包含工具名的格式，提取出来
        var match = Regex.Match(toolCallId, @"(\w+)_");
        if (match.Success)
            return match.Groups[1].Value;
        
        return toolCallId.Length > 10 ? toolCallId[..10] : toolCallId;
    }
    
    /// <summary>
    /// 估算 token 数量（粗略估算：中文约 1.5 token/字符，英文约 4 token/词）
    /// </summary>
    public int EstimateTokens(List<ChatMessage> messages)
    {
        int totalChars = 0;
        
        foreach (var msg in messages)
        {
            var content = msg.Content?.ToString() ?? "";
            totalChars += content.Length;
            
            // 加上 role 标签
            totalChars += msg.Role.Length;
            
            // 加上 tool_calls
            if (msg.ToolCalls != null)
            {
                foreach (var tc in msg.ToolCalls)
                {
                    totalChars += tc.Function?.Name?.Length ?? 0;
                    totalChars += tc.Function?.Arguments?.Length ?? 0;
                }
            }
        }
        
        // 粗略估算：中文 1.5 token/字符，英文 0.25 token/字符
        // 假设混合内容，取中间值
        return (int)(totalChars*1.5);
    }
    
    /// <summary>
    /// Layer 2: auto_compact - token 超过阈值时自动触发
    /// 保存完整对话到磁盘，让 LLM 摘要
    /// </summary>
    public async Task<List<ChatMessage>> AutoCompactAsync(List<ChatMessage> messages)
    {
        // 1. 保存完整对话到磁盘
        var transcriptPath = SaveTranscript(messages);
        
        ConsoleLogger.Info($"Auto-compacting context. Transcript saved to: {transcriptPath}");
        
        // 2. 让 LLM 摘要对话
        var summary = await SummarizeConversationAsync(messages);

        // 标记已执行自动压缩
        MarkAutoCompressed(summary);
        
        // 3. 替换为摘要
        var compressedMessages = new List<ChatMessage>
        {
            new ChatMessage
            {
                Role = "user",
                Content = $"[Compressed conversation]\n\n{summary}"
            },
            new ChatMessage
            {
                Role = "assistant", 
                Content = "Understood. Continuing from the compressed context."
            }
        };
        
        ConsoleLogger.Success("Context auto-compacted successfully");
        
        return compressedMessages;
    }
    
    /// <summary>
    /// Layer 3: compact 工具 - 手动触发压缩
    /// </summary>
    public async Task<string> CompactAsync(List<ChatMessage> messages)
    {
        // 先保存 transcript
        var transcriptPath = SaveTranscript(messages);
        
        ConsoleLogger.Info($"Manual compact triggered. Transcript saved to: {transcriptPath}");
        
        // 摘要对话
        var summary = await SummarizeConversationAsync(messages);
        
        return $"Context compacted successfully.\n\nTranscript saved to: {transcriptPath}\n\nSummary:\n{summary}";
    }
    
    /// <summary>
    /// 保存对话 transcript 到磁盘
    /// </summary>
    private string SaveTranscript(List<ChatMessage> messages)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var filename = $"transcript_{timestamp}.json";
        var filepath = Path.Combine(transcriptDirectory, filename);
        
        var json = JsonSerializer.Serialize(messages, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        
        File.WriteAllText(filepath, json);
        
        return filepath;
    }
    
    /// <summary>
    /// 使用 LLM 摘要对话
    /// </summary>
    private async Task<string> SummarizeConversationAsync(List<ChatMessage> messages)
    {
        // 构建摘要请求
        var summaryPrompt = @"Summarize this conversation for continuity. Include:
1. What the user asked for
2. What tools were used and what results were obtained
3. Any important decisions or conclusions
4. What remains to be done

Be concise but comprehensive. Use bullet points.";

        // 将消息转为简化的 JSON 格式
        var simplifiedMessages = messages.Select(m => new
        {
            role = m.Role,
            content = m.Content?.ToString()?.Length > 2000 
                ? m.Content?.ToString()?[..2000] + "..." 
                : m.Content?.ToString()
        }).ToList();
        
        var messagesJson = JsonSerializer.Serialize(simplifiedMessages);
        
        var request = new ChatRequest
        {
            Model = summaryModel,  // 使用当前配置的模型做摘要
            Messages = new List<ChatMessage>
            {
                new ChatMessage { Role = "user", Content = summaryPrompt + "\n\n" + messagesJson }
            },
            MaxTokens = 2000
        };
        
        try
        {
            var response = await client.ChatAsync(request);
            var choice = response.Choices.FirstOrDefault();
            return choice?.Message?.Content?.ToString() ?? "Failed to generate summary";
        }
        catch (Exception ex)
        {
            ConsoleLogger.Warning($"Summary generation failed: {ex.Message}");
            return $"Summary failed: {ex.Message}";
        }
    }
    
    /// <summary>
    /// 检查是否需要自动压缩
    /// </summary>
    public bool NeedsAutoCompact(List<ChatMessage> messages)
    {
        return EstimateTokens(messages) > TOKEN_THRESHOLD;
    }

    // 自动压缩状态
    private bool wasAutoCompressed = false;
    private string? lastSummary = null;

    /// <summary>
    /// 标记已执行自动压缩
    /// </summary>
    public void MarkAutoCompressed(string summary)
    {
        wasAutoCompressed = true;
        lastSummary = summary;
    }

    /// <summary>
    /// 检查是否刚刚执行了自动压缩
    /// </summary>
    public bool WasAutoCompressed()
    {
        return wasAutoCompressed;
    }

    /// <summary>
    /// 获取最后一次摘要
    /// </summary>
    public string? GetLastSummary()
    {
        return lastSummary;
    }

    /// <summary>
    /// 重置自动压缩状态
    /// </summary>
    public void ResetAutoCompressed()
    {
        wasAutoCompressed = false;
        lastSummary = null;
    }
}
