using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LearnAgent.Models;
using LearnAgent.Services;

namespace LearnAgent.Clients;

/// <summary>
/// OpenAI 兼容 API 客户端
/// </summary>
public class OpenAIClient : ILLMClient
{
    private readonly HttpClient httpClient;
    private readonly string? customEndpoint;
    private readonly string apiKey;
    
    public string Name => "OpenAI Compatible";
    
    public OpenAIClient(string apiKey, string? baseUrl = null)
    {
        // 清理 API Key，移除可能的空白字符
        this.apiKey = apiKey.Trim();
        httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5) // 设置超时为 5 分钟
        };
        customEndpoint = null;
        
        if (string.IsNullOrEmpty(baseUrl))
        {
            httpClient.BaseAddress = new Uri("https://api.openai.com");
        }
        else if (baseUrl.Contains("bigmodel.cn"))
        {
            // 智谱 AI 特殊处理
            httpClient.BaseAddress = new Uri(baseUrl);
            customEndpoint = "/api/paas/v4/chat/completions";
        }
        else
        {
            httpClient.BaseAddress = new Uri(baseUrl);
        }
    }
    
    public async Task<ChatResponse> ChatAsync(ChatRequest request)
    {
        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
        
        var json = JsonSerializer.Serialize(request, options);
        
        // 调试输出：使用格式化日志
        if (request.Messages != null && request.Messages.Count > 0)
        {
            var msgCount = request.Messages.Count;
            var showCount = Math.Min(3, msgCount);
            ConsoleLogger.RequestSummary(request.Model, msgCount, showCount);
            
            for (int i = msgCount - showCount; i < msgCount; i++)
            {
                var msg = request.Messages[i];
                var contentPreview = msg.Content?.ToString() ?? "";
                ConsoleLogger.Message(msg.Role, contentPreview, i);
            }
        }
        
        var endpoint = customEndpoint ?? "/v1/chat/completions";
        
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        
        // 使用 AuthenticationHeaderValue 设置 Authorization
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        
        // 显示等待提示
        ConsoleLogger.Info("正在请求 API...");
        
        var response = await httpClient.SendAsync(httpRequest);
        var content = await response.Content.ReadAsStringAsync();
        
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"API Error: {response.StatusCode} - {content}");
        }
        
        return JsonSerializer.Deserialize<ChatResponse>(content, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        }) ?? throw new Exception("Failed to deserialize response");
    }
    
    public void Dispose()
    {
        httpClient.Dispose();
    }
}
