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
/// - S11: 自治智能体 - WORK/IDLE双阶段、空闲轮询、自动认领任务、身份重注入
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
    
    // S11: 空闲状态追踪
    private readonly ConcurrentDictionary<string, bool> IdleFlags = new();
    private readonly ConcurrentDictionary<string, List<ChatMessage>> TeammateMessages = new();
    
    // 需要外部注入的依赖
    private ILLMClient? client;
    private ToolRegistry? toolRegistry;
    private string? modelId;
    private TaskManager? taskManager;
    
    // S11 常量
    private const int IDLE_TIMEOUT = 60; // 空闲超时（秒）
    private const int POLL_INTERVAL = 5; // 轮询间隔（秒）
    
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
    public void SetDependencies(ILLMClient client, ToolRegistry toolRegistry, string modelId, TaskManager? taskManager = null)
    {
        this.client = client;
        this.toolRegistry = toolRegistry;
        this.modelId = modelId;
        this.taskManager = taskManager;
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
        
        // 重置关闭标志和空闲标志
        ShutdownFlags[name] = false;
        IdleFlags[name] = false;
        
        // 初始化消息历史
        TeammateMessages[name] = new List<ChatMessage>
        {
            new() { Role = "user", Content = prompt }
        };
        
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
    /// S11: 队友的主循环 - WORK/IDLE 双阶段
    /// </summary>
    private void TeammateLoop(string name, string role, string initialPrompt)
    {
        try
        {
            var messages = TeammateMessages.GetOrAdd(name, _ => new List<ChatMessage>
            {
                new() { Role = "user", Content = initialPrompt }
            });
            
            while (!ShutdownFlags.GetValueOrDefault(name, false))
            {
                // ==================== WORK PHASE ====================
                UpdateMemberStatus(name, TeammateStatus.Working);
                IdleFlags[name] = false;
                
                var shouldIdle = WorkPhase(name, role, messages);
                
                if (ShutdownFlags.GetValueOrDefault(name, false))
                {
                    break;
                }
                
                // ==================== IDLE PHASE ====================
                if (shouldIdle)
                {
                    UpdateMemberStatus(name, TeammateStatus.Idle);
                    IdleFlags[name] = true;
                    
                    var resume = IdlePoll(name, role, messages);
                    
                    if (!resume)
                    {
                        // 超时 -> 自动关机
                        ConsoleLogger.Info($"Teammate '{name}' idle timeout, shutting down");
                        UpdateMemberStatus(name, TeammateStatus.Shutdown);
                        return;
                    }
                }
            }
            
            // 正常退出
            UpdateMemberStatus(name, TeammateStatus.Shutdown);
        }
        catch (Exception ex)
        {
            ConsoleLogger.Error($"Teammate '{name}' error: {ex.Message}");
            UpdateMemberStatus(name, TeammateStatus.Idle);
        }
    }

    /// <summary>
    /// S11: 工作阶段 - 执行任务直到停止
    /// </summary>
    private bool WorkPhase(string name, string role, List<ChatMessage> messages)
    {
        var teammateSystemPrompt = BuildTeammateSystemPrompt(name, role);
        
        for (int i = 0; i < 50; i++) // 最多50轮
        {
            // 检查关闭标志
            if (ShutdownFlags.GetValueOrDefault(name, false))
            {
                return false;
            }
            
            // S11: 身份重注入 - 如果消息过短（说明发生了压缩），重新注入身份
            if (messages.Count <= 3)
            {
                InjectIdentity(messages, name, role);
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
                bool idleRequested = false;
                
                foreach (var toolCall in assistantMessage.ToolCalls)
                {
                    // S11: 检查是否请求进入空闲状态
                    if (toolCall.Function?.Name == "idle")
                    {
                        idleRequested = true;
                        messages.Add(new ChatMessage
                        {
                            Role = "tool",
                            Content = "Entered idle state. Will poll for new tasks.",
                            ToolCallId = toolCall.Id
                        });
                    }
                    else
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
                
                // 如果调用了 idle 工具，返回进入空闲状态
                if (idleRequested)
                {
                    return true;
                }
            }
            else if (assistantMessage.Content != null)
            {
                // 没有工具调用，表示完成任务，进入空闲状态
                return true;
            }
        }
        
        return true; // 超过最大轮数，进入空闲状态
    }

    /// <summary>
    /// S11: 空闲阶段轮询 - 检查收件箱和任务看板
    /// </summary>
    private bool IdlePoll(string name, string role, List<ChatMessage> messages)
    {
        int pollCount = IDLE_TIMEOUT / POLL_INTERVAL; // 60s / 5s = 12 次
        
        for (int i = 0; i < pollCount; i++)
        {
            // 检查关闭标志
            if (ShutdownFlags.GetValueOrDefault(name, false))
            {
                return false;
            }
            
            Thread.Sleep(POLL_INTERVAL * 1000);
            
            // 1. 检查收件箱
            var inbox = MessageBus.ReadInbox(name);
            if (inbox != "[]")
            {
                messages.Add(new ChatMessage
                {
                    Role = "user",
                    Content = $"<inbox>\n{inbox}\n</inbox>"
                });
                ConsoleLogger.Info($"Teammate '{name}' received message, resuming work");
                return true;
            }
            
            // 2. S11: 扫描任务看板，自动认领未分配任务
            if (taskManager != null)
            {
                var unclaimed = taskManager.GetReadyTasks();
                if (unclaimed.Count > 0)
                {
                    var task = unclaimed[0];
                    var claimResult = taskManager.Claim(task.Id, name);
                    
                    messages.Add(new ChatMessage
                    {
                        Role = "user",
                        Content = $"<auto-claimed>Task #{task.Id}: {task.Subject}</auto-claimed>\n" +
                                  $"You have automatically claimed this task. Start working on it now."
                    });
                    
                    ConsoleLogger.Info($"Teammate '{name}' auto-claimed task #{task.Id}: {task.Subject}");
                    return true;
                }
            }
        }
        
        // 超时
        return false;
    }

    /// <summary>
    /// S11: 构建队友系统提示
    /// </summary>
    private string BuildTeammateSystemPrompt(string name, string role)
    {
        return $"You are {name}, a {role} in a team. " +
               "**IMPORTANT: You are on Windows. Use 'dir' instead of 'ls', 'type' instead of 'cat'.**\n" +
               "You can receive messages from other teammates via your inbox. " +
               "Use the 'send_message' tool to communicate with others. " +
               "Use 'read_inbox' to check for new messages. " +
               "Use 'shutdown_response' to respond to shutdown requests. " +
               "Use 'plan_submit' to submit plans for approval.\n" +
               "**S11 Autonomous Behavior:**\n" +
               "- Use the 'idle' tool when you have no immediate work to enter idle state\n" +
               "- While idle, you will automatically poll for new messages and tasks\n" +
               "- You can auto-claim unclaimed tasks from the task board\n" +
               "- Use 'claim_task' to manually claim a task by ID\n" +
               "- After 60 seconds of idle with no work, you will auto-shutdown\n" +
               "Work autonomously and report progress when asked.";
    }

    /// <summary>
    /// S11: 身份重注入
    /// </summary>
    private void InjectIdentity(List<ChatMessage> messages, string name, string role)
    {
        // 在开头插入身份块
        messages.Insert(0, new ChatMessage
        {
            Role = "user",
            Content = $"<identity>You are '{name}', role: {role}, team: {Config.Leader}'s team. " +
                      $"Continue your work. If you have no immediate task, use the 'idle' tool.</identity>"
        });
        messages.Insert(1, new ChatMessage
        {
            Role = "assistant",
            Content = $"I am {name}. Continuing."
        });
    }

    /// <summary>
    /// S11: 进入空闲状态
    /// </summary>
    public string EnterIdle(string name)
    {
        var member = Config.Members.FirstOrDefault(m => m.Name == name);
        if (member == null)
        {
            return $"Error: Teammate '{name}' not found";
        }
        
        IdleFlags[name] = true;
        return $"Teammate '{name}' entering idle state";
    }

    /// <summary>
    /// S11: 手动认领任务
    /// </summary>
    public string ClaimTask(string name, int taskId)
    {
        if (taskManager == null)
        {
            return "Error: TaskManager not initialized";
        }
        
        var member = Config.Members.FirstOrDefault(m => m.Name == name);
        if (member == null)
        {
            return $"Error: Teammate '{name}' not found";
        }
        
        return taskManager.Claim(taskId, name);
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
        
        // S11: 特殊处理 idle 工具
        if (toolName == "idle")
        {
            return EnterIdle(teammateName);
        }
        
        // S11: 特殊处理 claim_task 工具
        if (toolName == "claim_task")
        {
            return ExecuteClaimTask(teammateName, arguments);
        }
        
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
        else if (toolName == "file_task")
        {
            // S11: 处理任务完成时更新 owner
            return ExecuteFileTask(teammateName, arguments);
        }
        
        // 其他工具由 toolRegistry 执行
        var result = toolRegistry!.ExecuteAsync(toolName, arguments).Result;
        ConsoleLogger.ToolResult(result);
        return result;
    }

    /// <summary>
    /// S11: 执行认领任务
    /// </summary>
    private string ExecuteClaimTask(string teammateName, string argumentsJson)
    {
        try
        {
            var args = JsonSerializer.Deserialize<ClaimTaskArgs>(argumentsJson);
            if (args == null || args.TaskId <= 0)
            {
                return "Error: 'task_id' is required and must be positive";
            }
            
            return ClaimTask(teammateName, args.TaskId);
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private class ClaimTaskArgs
    {
        [System.Text.Json.Serialization.JsonPropertyName("task_id")]
        public int TaskId { get; set; }
    }

    /// <summary>
    /// S11: 执行文件任务工具（带 owner 更新）
    /// </summary>
    private string ExecuteFileTask(string teammateName, string argumentsJson)
    {
        try
        {
            var args = JsonSerializer.Deserialize<FileTaskArgs>(argumentsJson);
            if (args == null || string.IsNullOrEmpty(args.Action))
            {
                return "Error: action is required";
            }
            
            var action = args.Action.ToLowerInvariant();
            
            return action switch
            {
                "create" => taskManager!.Create(args.Subject ?? "", args.Description ?? ""),
                "get" => args.TaskId > 0 ? taskManager!.Get(args.TaskId) : "Error: task_id is required",
                "update" => args.TaskId > 0 
                    ? taskManager!.Update(args.TaskId, args.Status, args.AddBlockedBy, args.AddBlocks) 
                    : "Error: task_id is required",
                "list" => taskManager!.ListAll(),
                "claim" => args.TaskId > 0 
                    ? taskManager!.Claim(args.TaskId, args.Owner ?? teammateName) 
                    : "Error: task_id is required",
                "unclaim" => args.TaskId > 0 ? taskManager!.Unclaim(args.TaskId) : "Error: task_id is required",
                "delete" => args.TaskId > 0 ? taskManager!.Delete(args.TaskId) : "Error: task_id is required",
                _ => $"Error: Unknown action '{action}'"
            };
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private class FileTaskArgs
    {
        [System.Text.Json.Serialization.JsonPropertyName("action")]
        public string? Action { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("task_id")]
        public int TaskId { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("subject")]
        public string? Subject { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("description")]
        public string? Description { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("status")]
        public string? Status { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("owner")]
        public string? Owner { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("add_blocked_by")]
        public List<int>? AddBlockedBy { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("add_blocks")]
        public List<int>? AddBlocks { get; set; }
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
                TeammateStatus.Idle => "💤",
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
