using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LearnAgent.Models;

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
        httpClient = new HttpClient();
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
        
        // 调试输出
        Console.WriteLine($"[DEBUG] 发送请求: {json[..Math.Min(500, json.Length)]}...");
        
        var endpoint = customEndpoint ?? "/v1/chat/completions";
        
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        
        // 使用 AuthenticationHeaderValue 设置 Authorization
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        
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
