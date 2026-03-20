using System.Text.Json;
using System.Text.Json.Serialization;
using LearnAgent.Services;

namespace LearnAgent.Tools;

/// <summary>
/// 创建队友工具
/// </summary>
public class SpawnTool : ITool
{
    public string Name => "teammate_spawn";
    
    public string Description => 
        "Spawn a new teammate agent with a specific role. " +
        "Parameters: name (string) - unique name for the teammate, " +
        "role (string) - role description (e.g., 'coder', 'tester'), " +
        "prompt (string) - initial task or instruction for the teammate.";
    
    private readonly TeammateManager teammateManager;
    
    public SpawnTool(TeammateManager teammateManager)
    {
        this.teammateManager = teammateManager;
    }
    
    public Task<string> ExecuteAsync(string argumentsJson)
    {
        try
        {
            var args = JsonSerializer.Deserialize<SpawnArguments>(argumentsJson);
            if (args == null || string.IsNullOrEmpty(args.Name) || string.IsNullOrEmpty(args.Role))
            {
                return Task.FromResult("Error: 'name' and 'role' are required");
            }
            
            return Task.FromResult(teammateManager.Spawn(
                args.Name, 
                args.Role, 
                args.Prompt ?? "",
                args.Mode ?? "default"));
        }
        catch (Exception ex)
        {
            return Task.FromResult($"Error: {ex.Message}");
        }
    }
    
    private class SpawnArguments
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
        [JsonPropertyName("role")]
        public string? Role { get; set; }
        [JsonPropertyName("prompt")]
        public string? Prompt { get; set; }
        [JsonPropertyName("mode")]
        public string? Mode { get; set; }
    }
}

/// <summary>
/// 发送消息工具
/// </summary>
public class SendMessageTool : ITool
{
    public string Name => "send_message";
    
    public string Description => 
        "Send a message to a teammate. " +
        "Parameters: recipient (string) - name of the teammate to send to, " +
        "content (string) - message content.";
    
    private readonly TeammateManager teammateManager;
    
    public SendMessageTool(TeammateManager teammateManager)
    {
        this.teammateManager = teammateManager;
    }
    
    public Task<string> ExecuteAsync(string argumentsJson)
    {
        try
        {
            var args = JsonSerializer.Deserialize<SendMessageArguments>(argumentsJson);
            if (args == null || string.IsNullOrEmpty(args.Recipient) || string.IsNullOrEmpty(args.Content))
            {
                return Task.FromResult("Error: 'recipient' and 'content' are required");
            }
            
            return Task.FromResult(teammateManager.SendMessage(
                args.Sender ?? "lead",
                args.Recipient,
                args.Content));
        }
        catch (Exception ex)
        {
            return Task.FromResult($"Error: {ex.Message}");
        }
    }
    
    private class SendMessageArguments
    {
        [JsonPropertyName("sender")]
        public string? Sender { get; set; }
        [JsonPropertyName("recipient")]
        public string? Recipient { get; set; }
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}

/// <summary>
/// 广播消息工具
/// </summary>
public class BroadcastTool : ITool
{
    public string Name => "broadcast";
    
    public string Description => 
        "Broadcast a message to all teammates. " +
        "Parameters: content (string) - message content, " +
        "summary (string, optional) - short summary of the message.";
    
    private readonly TeammateManager teammateManager;
    
    public BroadcastTool(TeammateManager teammateManager)
    {
        this.teammateManager = teammateManager;
    }
    
    public Task<string> ExecuteAsync(string argumentsJson)
    {
        try
        {
            var args = JsonSerializer.Deserialize<BroadcastArguments>(argumentsJson);
            if (args == null || string.IsNullOrEmpty(args.Content))
            {
                return Task.FromResult("Error: 'content' is required");
            }
            
            return Task.FromResult(teammateManager.Broadcast(
                args.Sender ?? "lead",
                args.Content,
                args.Summary));
        }
        catch (Exception ex)
        {
            return Task.FromResult($"Error: {ex.Message}");
        }
    }
    
    private class BroadcastArguments
    {
        [JsonPropertyName("sender")]
        public string? Sender { get; set; }
        [JsonPropertyName("content")]
        public string? Content { get; set; }
        [JsonPropertyName("summary")]
        public string? Summary { get; set; }
    }
}

/// <summary>
/// 读取收件箱工具
/// </summary>
public class ReadInboxTool : ITool
{
    public string Name => "read_inbox";
    
    public string Description => 
        "Read and drain your inbox (messages are removed after reading). " +
        "Parameters: name (string, optional) - name to check (defaults to 'lead' for leader).";
    
    private readonly TeammateManager teammateManager;
    
    public ReadInboxTool(TeammateManager teammateManager)
    {
        this.teammateManager = teammateManager;
    }
    
    public Task<string> ExecuteAsync(string argumentsJson)
    {
        try
        {
            var args = JsonSerializer.Deserialize<ReadInboxArguments>(argumentsJson);
            var name = args?.Name ?? "lead";
            
            return Task.FromResult(teammateManager.ReadInbox(name));
        }
        catch (Exception ex)
        {
            return Task.FromResult($"Error: {ex.Message}");
        }
    }
    
    private class ReadInboxArguments
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}

/// <summary>
/// 团队状态工具
/// </summary>
public class TeamStatusTool : ITool
{
    public string Name => "team_status";
    
    public string Description => 
        "Get the current team roster and status of all teammates. " +
        "No parameters required.";
    
    private readonly TeammateManager teammateManager;
    
    public TeamStatusTool(TeammateManager teammateManager)
    {
        this.teammateManager = teammateManager;
    }
    
    public Task<string> ExecuteAsync(string argumentsJson)
    {
        return Task.FromResult(teammateManager.GetTeamStatus());
    }
}

/// <summary>
/// 关闭队友工具
/// </summary>
public class ShutdownTeammateTool : ITool
{
    public string Name => "teammate_shutdown";
    
    public string Description => 
        "Shutdown a teammate gracefully. " +
        "Parameters: name (string) - name of the teammate to shutdown.";
    
    private readonly TeammateManager teammateManager;
    
    public ShutdownTeammateTool(TeammateManager teammateManager)
    {
        this.teammateManager = teammateManager;
    }
    
    public Task<string> ExecuteAsync(string argumentsJson)
    {
        try
        {
            var args = JsonSerializer.Deserialize<ShutdownArguments>(argumentsJson);
            if (args == null || string.IsNullOrEmpty(args.Name))
            {
                return Task.FromResult("Error: 'name' is required");
            }
            
            return Task.FromResult(teammateManager.Shutdown(args.Name));
        }
        catch (Exception ex)
        {
            return Task.FromResult($"Error: {ex.Message}");
        }
    }
    
    private class ShutdownArguments
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}
