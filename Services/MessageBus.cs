using System.Collections.Concurrent;
using System.Text.Json;
using LearnAgent.Models;

namespace LearnAgent.Services;

/// <summary>
/// 消息总线 - JSONL 收件箱实现
/// 
/// 功能：
/// - append-only 写入消息
/// - read + drain 读取模式（读后清空）
/// - 支持点对点消息和广播
/// </summary>
public class MessageBus
{
    private readonly string InboxDirectory;
    
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public MessageBus(string teamDirectory)
    {
        InboxDirectory = Path.Combine(teamDirectory, "inbox");
        Directory.CreateDirectory(InboxDirectory);
    }

    /// <summary>
    /// 发送消息给指定队友
    /// </summary>
    /// <param name="sender">发送者</param>
    /// <param name="to">接收者</param>
    /// <param name="content">消息内容</param>
    /// <param name="msgType">消息类型</param>
    /// <param name="extra">额外信息</param>
    public string Send(string sender, string to, string content, string msgType = MessageType.Message, Dictionary<string, object>? extra = null)
    {
        var message = new TeamMessage
        {
            Type = msgType,
            From = sender,
            To = to,
            Content = content,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0,
            Extra = extra
        };
        
        return WriteMessage(to, message);
    }

    /// <summary>
    /// 广播消息给所有队友
    /// </summary>
    /// <param name="sender">发送者</param>
    /// <param name="content">消息内容</param>
    /// <param name="summary">消息摘要</param>
    public string Broadcast(string sender, string content, string? summary = null)
    {
        // 广播消息不指定接收者，需要 TeammateManager 分发给所有队友
        var message = new TeamMessage
        {
            Type = MessageType.Broadcast,
            From = sender,
            To = "", // 广播没有特定接收者
            Content = content,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0,
            Extra = summary != null ? new Dictionary<string, object> { ["summary"] = summary } : null
        };
        
        // 写入所有收件箱（由 TeammateManager 调用）
        return JsonSerializer.Serialize(message, JsonOpts);
    }

    /// <summary>
    /// 发送关闭请求
    /// </summary>
    public string SendShutdownRequest(string sender, string to, string reason = "")
    {
        var message = new TeamMessage
        {
            Type = MessageType.Shutdown,
            From = sender,
            To = to,
            Content = reason,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0
        };
        
        return WriteMessage(to, message);
    }

    /// <summary>
    /// 发送关闭响应
    /// </summary>
    public string SendShutdownResponse(string sender, string to, bool approve, string? requestId = null)
    {
        var message = new TeamMessage
        {
            Type = MessageType.ShutdownResponse,
            From = sender,
            To = to,
            Content = approve ? "Shutdown approved" : "Shutdown rejected",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0,
            Approve = approve,
            RequestId = requestId
        };
        
        return WriteMessage(to, message);
    }

    /// <summary>
    /// 写入消息到收件箱
    /// </summary>
    private string WriteMessage(string recipient, TeamMessage message)
    {
        var inboxPath = GetInboxPath(recipient);
        var jsonLine = JsonSerializer.Serialize(message, JsonOpts);
        
        // append-only 写入
        File.AppendAllText(inboxPath, jsonLine + "\n");
        
        return $"Message sent to {recipient}";
    }

    /// <summary>
    /// 读取收件箱并清空（drain-on-read）
    /// </summary>
    /// <param name="name">队友名称</param>
    /// <returns>消息列表 JSON</returns>
    public string ReadInbox(string name)
    {
        var inboxPath = GetInboxPath(name);
        
        if (!File.Exists(inboxPath))
        {
            return "[]";
        }
        
        // 读取所有行
        var lines = File.ReadAllLines(inboxPath);
        
        // 清空文件（drain）
        File.WriteAllText(inboxPath, "");
        
        if (lines.Length == 0)
        {
            return "[]";
        }
        
        // 解析消息
        var messages = new List<TeamMessage>();
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            
            try
            {
                var msg = JsonSerializer.Deserialize<TeamMessage>(line, JsonOpts);
                if (msg != null)
                {
                    messages.Add(msg);
                }
            }
            catch
            {
                // 忽略解析错误
            }
        }
        
        return JsonSerializer.Serialize(messages, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    /// <summary>
    /// 获取收件箱路径
    /// </summary>
    private string GetInboxPath(string name)
    {
        return Path.Combine(InboxDirectory, $"{name}.jsonl");
    }

    /// <summary>
    /// 检查收件箱是否有消息
    /// </summary>
    public bool HasMessages(string name)
    {
        var inboxPath = GetInboxPath(name);
        if (!File.Exists(inboxPath)) return false;
        
        var content = File.ReadAllText(inboxPath);
        return !string.IsNullOrWhiteSpace(content);
    }

    /// <summary>
    /// 清理所有收件箱
    /// </summary>
    public void ClearAll()
    {
        foreach (var file in Directory.GetFiles(InboxDirectory, "*.jsonl"))
        {
            File.Delete(file);
        }
    }
}
