using System.Collections.Concurrent;
using System.Text.Json;
using LearnAgent.Clients;
using LearnAgent.Models;
using LearnAgent.Tools;

namespace LearnAgent.Services;

/// <summary>
/// 队友管理器 - 管理智能体团队
/// 
/// 功能：
/// - 创建/销毁队友
/// - 管理队友生命周期
/// - 运行队友的 agent loop
/// - 协调队友间通信
/// - S10: 关机协议和计划审批
/// </summary>
public class TeammateManager
{
    private readonly string TeamDirectory;
    private readonly string ConfigPath;
    private TeamConfig Config;
    private readonly MessageBus MessageBus;
    private readonly ConcurrentDictionary<string, Thread> Threads = new();
    private readonly ConcurrentDictionary<string, bool> ShutdownFlags = new();
    
    // S10: 协调请求追踪
    private readonly ConcurrentDictionary<string, CoordinationRequest> ShutdownRequests = new();
    private readonly ConcurrentDictionary<string, CoordinationRequest> PlanRequests = new();
    
    // 需要外部注入的依赖
    private ILLMClient? client;
    private ToolRegistry? toolRegistry;
    private string? modelId;
    
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public TeammateManager(string workDirectory)
    {
        TeamDirectory = Path.Combine(workDirectory, ".team");
        Directory.CreateDirectory(TeamDirectory);
        
        ConfigPath = Path.Combine(TeamDirectory, "config.json");
        Config = LoadConfig();
        
        MessageBus = new MessageBus(TeamDirectory);
    }

    /// <summary>
    /// 设置依赖（由 Program.cs 注入）
    /// </summary>
    public void SetDependencies(ILLMClient client, ToolRegistry toolRegistry, string modelId)
    {
        this.client = client;
        this.toolRegistry = toolRegistry;
        this.modelId = modelId;
    }

    /// <summary>
    /// 加载团队配置
    /// </summary>
    private TeamConfig LoadConfig()
    {
        if (!File.Exists(ConfigPath))
        {
            var config = new TeamConfig
            {
                Leader = "lead",
                CreatedAt = DateTime.UtcNow
            };
            SaveConfig(config);
            return config;
        }
        
        var json = File.ReadAllText(ConfigPath);
        return JsonSerializer.Deserialize<TeamConfig>(json, JsonOpts) ?? new TeamConfig();
    }

    /// <summary>
    /// 保存团队配置
    /// </summary>
    private void SaveConfig(TeamConfig config)
    {
        var json = JsonSerializer.Serialize(config, JsonOpts);
        File.WriteAllText(ConfigPath, json);
    }

    /// <summary>
    /// 创建队友
    /// </summary>
    public string Spawn(string name, string role, string prompt, string mode = "default")
    {
        if (client == null || toolRegistry == null || modelId == null)
        {
            return "Error: TeammateManager not initialized. Call SetDependencies first.";
        }
        
        // 检查是否已存在
        if (Config.Members.Any(m => m.Name == name))
        {
            return $"Error: Teammate '{name}' already exists";
        }
        
        // 创建队友信息
        var teammate = new Teammate
        {
            Name = name,
            Role = role,
            Status = TeammateStatus.Working,
            SystemPrompt = $"You are {name}, a {role}. {prompt}",
            CreatedAt = DateTime.UtcNow
        };
        
        Config.Members.Add(teammate);
        SaveConfig(Config);
        
        // 重置关闭标志
        ShutdownFlags[name] = false;
        
        // 启动队友线程
        var thread = new Thread(() => TeammateLoop(name, role, prompt))
        {
            IsBackground = true,
            Name = $"teammate-{name}"
        };
        Threads[name] = thread;
        thread.Start();
        
        return $"Spawned teammate '{name}' (role: {role}, mode: {mode})";
    }

    /// <summary>
    /// 队友的主循环
    /// </summary>
    private void TeammateLoop(string name, string role, string initialPrompt)
    {
        try
        {
            var messages = new List<ChatMessage>
            {
                new() { Role = "user", Content = initialPrompt }
            };
            
            var teammateSystemPrompt = $"You are {name}, a {role} in a team. " +
                "**IMPORTANT: You are on Windows. Use 'dir' instead of 'ls', 'type' instead of 'cat'.** " +
                "You can receive messages from other teammates via your inbox. " +
                "Use the 'send_message' tool to communicate with others. " +
                "Use 'read_inbox' to check for new messages. " +
                "Use 'shutdown_response' to respond to shutdown requests. " +
                "Use 'plan_submit' to submit plans for approval. " +
                "Work autonomously and report progress when asked.";
            
            for (int i = 0; i < 50; i++) // 最多50轮
            {
                // 检查关闭标志
                if (ShutdownFlags.GetValueOrDefault(name, false))
                {
                    UpdateMemberStatus(name, TeammateStatus.Shutdown);
                    break;
                }
                
                // 读取收件箱
                var inbox = MessageBus.ReadInbox(name);
                if (inbox != "[]")
                {
                    messages.Add(new ChatMessage
                    {
                        Role = "user",
                        Content = $"<inbox>\n{inbox}\n</inbox>"
                    });
                    messages.Add(new ChatMessage
                    {
                        Role = "assistant",
                        Content = "Noted inbox messages."
                    });
                }
                
                // 构建请求
                var requestMessages = new List<ChatMessage>
                {
                    new() { Role = "system", Content = teammateSystemPrompt }
                };
                requestMessages.AddRange(messages);
                
                // 检查是否有消息（没有则跳过）
                if (inbox == "[]" && i > 0)
                {
                    // 等待新消息
                    Thread.Sleep(1000);
                    i--; // 不计数
                    continue;
                }
                
                var request = new ChatRequest
                {
                    Model = modelId!,
                    Messages = requestMessages,
                    MaxTokens = 2048,
                    Tools = toolRegistry!.GetToolDefinitions()
                };
                
                var response = client!.ChatAsync(request).Result;
                var choice = response.Choices.FirstOrDefault();
                
                if (choice == null) break;
                
                var assistantMessage = choice.Message;
                messages.Add(new ChatMessage
                {
                    Role = "assistant",
                    Content = assistantMessage.Content,
                    ToolCalls = assistantMessage.ToolCalls
                });
                
                // 处理工具调用
                if (assistantMessage.ToolCalls != null && assistantMessage.ToolCalls.Count > 0)
                {
                    foreach (var toolCall in assistantMessage.ToolCalls)
                    {
                        var result = ExecuteTeammateTool(name, toolCall);
                        messages.Add(new ChatMessage
                        {
                            Role = "tool",
                            Content = result,
                            ToolCallId = toolCall.Id
                        });
                    }
                }
                else if (assistantMessage.Content != null)
                {
                    // 没有工具调用，表示完成任务
                    break;
                }
            }
            
            // 标记为空闲
            UpdateMemberStatus(name, TeammateStatus.Idle);
        }
        catch (Exception ex)
        {
            ConsoleLogger.Error($"Teammate '{name}' error: {ex.Message}");
            UpdateMemberStatus(name, TeammateStatus.Idle);
        }
    }

    /// <summary>
    /// 执行队友的工具调用
    /// </summary>
    private string ExecuteTeammateTool(string teammateName, ToolCall toolCall)
    {
        if (toolCall.Function == null) return "Error: No function in tool call";
        
        var toolName = toolCall.Function.Name;
        var arguments = toolCall.Function.Arguments;
        
        ConsoleLogger.Tool($"{teammateName}/{toolName}", arguments);
        
        // 特殊处理团队相关工具
        if (toolName == "send_message")
        {
            return ExecuteSendMessage(teammateName, arguments);
        }
        else if (toolName == "read_inbox")
        {
            return MessageBus.ReadInbox(teammateName);
        }
        else if (toolName == "shutdown_response")
        {
            return ExecuteShutdownResponse(teammateName, arguments);
        }
        else if (toolName == "plan_submit")
        {
            return ExecutePlanSubmit(teammateName, arguments);
        }
        
        // 其他工具由 toolRegistry 执行
        var result = toolRegistry!.ExecuteAsync(toolName, arguments).Result;
        ConsoleLogger.ToolResult(result);
        return result;
    }

    /// <summary>
    /// 执行发送消息
    /// </summary>
    private string ExecuteSendMessage(string sender, string argumentsJson)
    {
        try
        {
            var args = JsonSerializer.Deserialize<SendMessageArgs>(argumentsJson);
            if (args == null) return "Error: Invalid arguments";
            
            return MessageBus.Send(sender, args.Recipient ?? "", args.Content ?? "", args.Type ?? MessageType.Message);
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private class SendMessageArgs
    {
        [System.Text.Json.Serialization.JsonPropertyName("recipient")]
        public string? Recipient { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("content")]
        public string? Content { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("type")]
        public string? Type { get; set; }
    }

    /// <summary>
    /// 执行关机响应
    /// </summary>
    private string ExecuteShutdownResponse(string sender, string argumentsJson)
    {
        try
        {
            var args = JsonSerializer.Deserialize<ShutdownResponseArgs>(argumentsJson);
            if (args == null || string.IsNullOrEmpty(args.RequestId))
            {
                return "Error: 'request_id' is required";
            }
            
            return SendShutdownResponse(sender, "lead", args.Approve, args.RequestId, args.Reason);
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private class ShutdownResponseArgs
    {
        [System.Text.Json.Serialization.JsonPropertyName("request_id")]
        public string? RequestId { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("approve")]
        public bool Approve { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("reason")]
        public string? Reason { get; set; }
    }

    /// <summary>
    /// 执行计划提交
    /// </summary>
    private string ExecutePlanSubmit(string sender, string argumentsJson)
    {
        try
        {
            var args = JsonSerializer.Deserialize<PlanSubmitArgs>(argumentsJson);
            if (args == null || string.IsNullOrEmpty(args.Plan))
            {
                return "Error: 'plan' is required";
            }
            
            return SubmitPlan(sender, args.Recipient ?? "lead", args.Plan, args.Summary);
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private class PlanSubmitArgs
    {
        [System.Text.Json.Serialization.JsonPropertyName("recipient")]
        public string? Recipient { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("plan")]
        public string? Plan { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("summary")]
        public string? Summary { get; set; }
    }

    /// <summary>
    /// 更新队友状态
    /// </summary>
    private void UpdateMemberStatus(string name, string status)
    {
        var member = Config.Members.FirstOrDefault(m => m.Name == name);
        if (member != null)
        {
            member.Status = status;
            member.LastActiveAt = DateTime.UtcNow;
            SaveConfig(Config);
        }
    }

    /// <summary>
    /// 获取团队状态
    /// </summary>
    public string GetTeamStatus()
    {
        var lines = new List<string>();
        lines.Add("Team Members:");
        lines.Add($"  Leader: {Config.Leader}");
        
        foreach (var member in Config.Members)
        {
            var statusIcon = member.Status switch
            {
                TeammateStatus.Working => "⏳",
                TeammateStatus.Idle => "✅",
                TeammateStatus.Shutdown => "❌",
                _ => "❓"
            };
            lines.Add($"  {statusIcon} {member.Name} ({member.Role}) - {member.Status}");
        }
        
        return string.Join("\n", lines);
    }

    /// <summary>
    /// 发送消息
    /// </summary>
    public string SendMessage(string sender, string to, string content)
    {
        return MessageBus.Send(sender, to, content);
    }

    /// <summary>
    /// 广播消息
    /// </summary>
    public string Broadcast(string sender, string content, string? summary = null)
    {
        // 分发给所有队友
        int count = 0;
        foreach (var member in Config.Members)
        {
            if (member.Name != sender && member.Status != TeammateStatus.Shutdown)
            {
                MessageBus.Send(sender, member.Name, content, MessageType.Broadcast);
                count++;
            }
        }
        
        return $"Broadcast to {count} teammates: {summary ?? content}";
    }

    /// <summary>
    /// 读取收件箱
    /// </summary>
    public string ReadInbox(string name)
    {
        return MessageBus.ReadInbox(name);
    }

    /// <summary>
    /// 关闭队友（强制）
    /// </summary>
    public string Shutdown(string name)
    {
        var member = Config.Members.FirstOrDefault(m => m.Name == name);
        if (member == null)
        {
            return $"Error: Teammate '{name}' not found";
        }
        
        ShutdownFlags[name] = true;
        return $"Shutdown request sent to '{name}'";
    }

    /// <summary>
    /// 删除队友
    /// </summary>
    public string Remove(string name)
    {
        var member = Config.Members.FirstOrDefault(m => m.Name == name);
        if (member == null)
        {
            return $"Error: Teammate '{name}' not found";
        }
        
        ShutdownFlags[name] = true;
        
        // 等待线程结束
        if (Threads.TryGetValue(name, out var thread))
        {
            thread.Join(2000);
        }
        
        Config.Members.Remove(member);
        SaveConfig(Config);
        
        return $"Teammate '{name}' removed";
    }

    /// <summary>
    /// 获取消息总线
    /// </summary>
    public MessageBus GetMessageBus()
    {
        return MessageBus;
    }
    
    // ==================== S10 Coordination Protocol ====================
    
    /// <summary>
    /// 协调请求
    /// </summary>
    private class CoordinationRequest
    {
        public string RequestId { get; set; } = "";
        public string From { get; set; } = "";
        public string To { get; set; } = "";
        public string Content { get; set; } = "";
        public string Status { get; set; } = "pending";  // pending, approved, rejected
        public DateTime CreatedAt { get; set; }
    }
    
    /// <summary>
    /// 发送关机请求 (领导 -> 队友)
    /// </summary>
    public string SendShutdownRequest(string sender, string recipient, string reason)
    {
        var member = Config.Members.FirstOrDefault(m => m.Name == recipient);
        if (member == null)
        {
            return $"Error: Teammate '{recipient}' not found";
        }
        
        var requestId = Guid.NewGuid().ToString()[..8];
        
        var request = new CoordinationRequest
        {
            RequestId = requestId,
            From = sender,
            To = recipient,
            Content = reason,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };
        
        ShutdownRequests[requestId] = request;
        
        // 通过消息总线发送请求
        MessageBus.Send(sender, recipient, reason, MessageType.Shutdown, new Dictionary<string, object>
        {
            ["request_id"] = requestId
        });
        
        return $"Shutdown request sent to '{recipient}' (request_id: {requestId})";
    }
    
    /// <summary>
    /// 发送关机响应 (队友 -> 领导)
    /// </summary>
    public string SendShutdownResponse(string sender, string recipient, bool approve, string requestId, string? reason = null)
    {
        if (!ShutdownRequests.TryGetValue(requestId, out var request))
        {
            return $"Error: Request '{requestId}' not found";
        }
        
        request.Status = approve ? "approved" : "rejected";
        
        // 通过消息总线发送响应
        MessageBus.Send(sender, recipient, reason ?? "", MessageType.ShutdownResponse, new Dictionary<string, object>
        {
            ["request_id"] = requestId,
            ["approve"] = approve
        });
        
        // 如果批准，设置关闭标志
        if (approve)
        {
            ShutdownFlags[sender] = true;
        }
        
        return $"Shutdown response sent: {(approve ? "approved" : "rejected")}";
    }
    
    /// <summary>
    /// 提交计划 (队友 -> 领导)
    /// </summary>
    public string SubmitPlan(string sender, string recipient, string plan, string? summary = null)
    {
        var requestId = Guid.NewGuid().ToString()[..8];
        
        var request = new CoordinationRequest
        {
            RequestId = requestId,
            From = sender,
            To = recipient,
            Content = plan,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };
        
        PlanRequests[requestId] = request;
        
        // 通过消息总线发送计划请求
        MessageBus.Send(sender, recipient, plan, MessageType.PlanApproval, new Dictionary<string, object>
        {
            ["request_id"] = requestId,
            ["summary"] = summary ?? plan[..Math.Min(50, plan.Length)]
        });
        
        return $"Plan submitted (request_id: {requestId})";
    }
    
    /// <summary>
    /// 审批计划 (领导 -> 队友)
    /// </summary>
    public string ReviewPlan(string requestId, bool approve, string feedback)
    {
        if (!PlanRequests.TryGetValue(requestId, out var request))
        {
            return $"Error: Request '{requestId}' not found";
        }
        
        request.Status = approve ? "approved" : "rejected";
        
        // 通过消息总线发送审批响应
        MessageBus.Send(request.To, request.From, feedback, MessageType.PlanApprovalResponse, new Dictionary<string, object>
        {
            ["request_id"] = requestId,
            ["approve"] = approve
        });
        
        return $"Plan {(approve ? "approved" : "rejected")} (request_id: {requestId})";
    }
    
    /// <summary>
    /// 获取请求状态
    /// </summary>
    public string GetRequestStatus(string requestId)
    {
        if (ShutdownRequests.TryGetValue(requestId, out var shutdownReq))
        {
            return $"Shutdown request {requestId}: {shutdownReq.Status}";
        }
        
        if (PlanRequests.TryGetValue(requestId, out var planReq))
        {
            return $"Plan request {requestId}: {planReq.Status}";
        }
        
        return $"Request {requestId} not found";
    }
    
    /// <summary>
    /// 列出待处理的请求
    /// </summary>
    public string ListPendingRequests()
    {
        var lines = new List<string>();
        
        var pendingShutdowns = ShutdownRequests.Values.Where(r => r.Status == "pending").ToList();
        var pendingPlans = PlanRequests.Values.Where(r => r.Status == "pending").ToList();
        
        if (pendingShutdowns.Count == 0 && pendingPlans.Count == 0)
        {
            return "No pending requests.";
        }
        
        if (pendingShutdowns.Count > 0)
        {
            lines.Add("Pending Shutdown Requests:");
            foreach (var req in pendingShutdowns)
            {
                lines.Add($"  [{req.RequestId}] {req.From} -> {req.To}: {req.Content[..Math.Min(50, req.Content.Length)]}");
            }
        }
        
        if (pendingPlans.Count > 0)
        {
            lines.Add("Pending Plan Requests:");
            foreach (var req in pendingPlans)
            {
                lines.Add($"  [{req.RequestId}] {req.From} -> {req.To}: {req.Content[..Math.Min(50, req.Content.Length)]}");
            }
        }
        
        return string.Join("\n", lines);
    }
}
