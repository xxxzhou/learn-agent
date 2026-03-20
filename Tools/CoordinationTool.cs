using System.Text.Json;
using System.Text.Json.Serialization;
using LearnAgent.Models;
using LearnAgent.Services;

namespace LearnAgent.Tools;

/// <summary>
/// 关机请求工具 - 领导向队友发送关机请求
/// </summary>
public class ShutdownRequestTool : ITool
{
    public string Name => "shutdown_request";
    
    public string Description => 
        "Request a teammate to shutdown gracefully. " +
        "The teammate can approve (finish work and exit) or reject (continue working). " +
        "Parameters: recipient (string) - name of the teammate, " +
        "reason (string, optional) - reason for shutdown.";
    
    private readonly TeammateManager teammateManager;
    
    public ShutdownRequestTool(TeammateManager teammateManager)
    {
        this.teammateManager = teammateManager;
    }
    
    public Task<string> ExecuteAsync(string argumentsJson)
    {
        try
        {
            var args = JsonSerializer.Deserialize<ShutdownRequestArgs>(argumentsJson);
            if (args == null || string.IsNullOrEmpty(args.Recipient))
            {
                return Task.FromResult("Error: 'recipient' is required");
            }
            
            return Task.FromResult(teammateManager.SendShutdownRequest(
                args.Sender ?? "lead",
                args.Recipient,
                args.Reason ?? ""));
        }
        catch (Exception ex)
        {
            return Task.FromResult($"Error: {ex.Message}");
        }
    }
    
    private class ShutdownRequestArgs
    {
        [JsonPropertyName("sender")]
        public string? Sender { get; set; }
        [JsonPropertyName("recipient")]
        public string? Recipient { get; set; }
        [JsonPropertyName("reason")]
        public string? Reason { get; set; }
    }
}

/// <summary>
/// 关机响应工具 - 队友响应关机请求
/// </summary>
public class ShutdownResponseTool : ITool
{
    public string Name => "shutdown_response";
    
    public string Description => 
        "Respond to a shutdown request from the leader. " +
        "Parameters: request_id (string) - the request ID from the shutdown request, " +
        "approve (boolean) - whether to approve shutdown, " +
        "reason (string, optional) - additional message.";
    
    private readonly TeammateManager teammateManager;
    
    public ShutdownResponseTool(TeammateManager teammateManager)
    {
        this.teammateManager = teammateManager;
    }
    
    public Task<string> ExecuteAsync(string argumentsJson)
    {
        try
        {
            var args = JsonSerializer.Deserialize<ShutdownResponseArgs>(argumentsJson);
            if (args == null || string.IsNullOrEmpty(args.RequestId))
            {
                return Task.FromResult("Error: 'request_id' is required");
            }
            
            return Task.FromResult(teammateManager.SendShutdownResponse(
                args.Sender ?? "teammate",
                args.Recipient ?? "lead",
                args.Approve,
                args.RequestId,
                args.Reason));
        }
        catch (Exception ex)
        {
            return Task.FromResult($"Error: {ex.Message}");
        }
    }
    
    private class ShutdownResponseArgs
    {
        [JsonPropertyName("sender")]
        public string? Sender { get; set; }
        [JsonPropertyName("recipient")]
        public string? Recipient { get; set; }
        [JsonPropertyName("request_id")]
        public string? RequestId { get; set; }
        [JsonPropertyName("approve")]
        public bool Approve { get; set; }
        [JsonPropertyName("reason")]
        public string? Reason { get; set; }
    }
}

/// <summary>
/// 计划提交工具 - 队友向领导提交计划审批
/// </summary>
public class PlanSubmitTool : ITool
{
    public string Name => "plan_submit";
    
    public string Description => 
        "Submit a plan to the leader for approval. " +
        "Parameters: plan (string) - the plan description, " +
        "summary (string, optional) - short summary of the plan.";
    
    private readonly TeammateManager teammateManager;
    
    public PlanSubmitTool(TeammateManager teammateManager)
    {
        this.teammateManager = teammateManager;
    }
    
    public Task<string> ExecuteAsync(string argumentsJson)
    {
        try
        {
            var args = JsonSerializer.Deserialize<PlanSubmitArgs>(argumentsJson);
            if (args == null || string.IsNullOrEmpty(args.Plan))
            {
                return Task.FromResult("Error: 'plan' is required");
            }
            
            return Task.FromResult(teammateManager.SubmitPlan(
                args.Sender ?? "teammate",
                args.Recipient ?? "lead",
                args.Plan,
                args.Summary));
        }
        catch (Exception ex)
        {
            return Task.FromResult($"Error: {ex.Message}");
        }
    }
    
    private class PlanSubmitArgs
    {
        [JsonPropertyName("sender")]
        public string? Sender { get; set; }
        [JsonPropertyName("recipient")]
        public string? Recipient { get; set; }
        [JsonPropertyName("plan")]
        public string? Plan { get; set; }
        [JsonPropertyName("summary")]
        public string? Summary { get; set; }
    }
}

/// <summary>
/// 计划审批工具 - 领导审批队友的计划
/// </summary>
public class PlanReviewTool : ITool
{
    public string Name => "plan_review";
    
    public string Description => 
        "Review and approve/reject a plan submitted by a teammate. " +
        "Parameters: request_id (string) - the request ID from the plan submission, " +
        "approve (boolean) - whether to approve the plan, " +
        "feedback (string, optional) - feedback or instructions.";
    
    private readonly TeammateManager teammateManager;
    
    public PlanReviewTool(TeammateManager teammateManager)
    {
        this.teammateManager = teammateManager;
    }
    
    public Task<string> ExecuteAsync(string argumentsJson)
    {
        try
        {
            var args = JsonSerializer.Deserialize<PlanReviewArgs>(argumentsJson);
            if (args == null || string.IsNullOrEmpty(args.RequestId))
            {
                return Task.FromResult("Error: 'request_id' is required");
            }
            
            return Task.FromResult(teammateManager.ReviewPlan(
                args.RequestId,
                args.Approve,
                args.Feedback ?? ""));
        }
        catch (Exception ex)
        {
            return Task.FromResult($"Error: {ex.Message}");
        }
    }
    
    private class PlanReviewArgs
    {
        [JsonPropertyName("request_id")]
        public string? RequestId { get; set; }
        [JsonPropertyName("approve")]
        public bool Approve { get; set; }
        [JsonPropertyName("feedback")]
        public string? Feedback { get; set; }
    }
}
