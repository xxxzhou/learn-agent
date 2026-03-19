using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LearnAgent.Models;

namespace LearnAgent.Clients;

/// <summary>
/// Google Gemini API 客户端
/// </summary>
public class GeminiClient : ILLMClient
{
    private readonly HttpClient httpClient;
    private readonly string modelId;
    private readonly string apiKey;
    
    public string Name => "Google Gemini";
    
    public GeminiClient(string apiKey, string modelId, string? baseUrl = null)
    {
        this.apiKey = apiKey;
        this.modelId = modelId;
        httpClient = new HttpClient();
        
        var baseUri = baseUrl ?? "https://generativelanguage.googleapis.com";
        httpClient.BaseAddress = new Uri(baseUri);
    }
    
    public async Task<ChatResponse> ChatAsync(ChatRequest request)
    {
        var geminiRequest = ConvertToGeminiFormat(request);
        
        var json = JsonSerializer.Serialize(geminiRequest, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
        
        Console.WriteLine($"[DEBUG] 发送请求: {json[..Math.Min(500, json.Length)]}...");
        
        var endpoint = $"/v1beta/models/{modelId}:generateContent?key={apiKey}";
        
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        
        var response = await httpClient.SendAsync(httpRequest);
        var content = await response.Content.ReadAsStringAsync();
        
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"API Error: {response.StatusCode} - {content}");
        }
        
        return ConvertFromGeminiFormat(content, request.Model);
    }
    
    private object ConvertToGeminiFormat(ChatRequest request)
    {
        var contents = new List<object>();
        string? systemInstruction = null;
        
        foreach (var msg in request.Messages)
        {
            if (msg.Role == "system")
            {
                systemInstruction = msg.Content?.ToString();
                continue;
            }
            
            var role = msg.Role == "assistant" ? "model" : "user";
            var parts = new List<object>();
            
            if (msg.Content != null)
            {
                parts.Add(new { text = msg.Content.ToString() });
            }
            
            contents.Add(new { role, parts });
        }
        
        var geminiRequest = new Dictionary<string, object>
        {
            ["contents"] = contents
        };
        
        if (!string.IsNullOrEmpty(systemInstruction))
        {
            geminiRequest["systemInstruction"] = new
            {
                parts = new[] { new { text = systemInstruction } }
            };
        }
        
        if (request.Tools != null && request.Tools.Count > 0)
        {
            var functionDeclarations = request.Tools
                .Where(t => t.Function != null)
                .Select(t => new
                {
                    name = t.Function!.Name,
                    description = t.Function.Description,
                    parameters = t.Function.Parameters
                })
                .ToList();
            
            if (functionDeclarations.Count > 0)
            {
                geminiRequest["tools"] = new[]
                {
                    new { functionDeclarations }
                };
            }
        }
        
        geminiRequest["generationConfig"] = new
        {
            maxOutputTokens = request.MaxTokens
        };
        
        return geminiRequest;
    }
    
    private ChatResponse ConvertFromGeminiFormat(string json, string model)
    {
        var geminiResponse = JsonSerializer.Deserialize<JsonElement>(json);
        
        var candidates = geminiResponse.GetProperty("candidates");
        var firstCandidate = candidates[0];
        var content = firstCandidate.GetProperty("content");
        var parts = content.GetProperty("parts");
        
        var responseText = "";
        var toolCalls = new List<ToolCall>();
        
        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("text", out var textProp))
            {
                responseText = textProp.GetString() ?? "";
            }
            else if (part.TryGetProperty("functionCall", out var funcCall))
            {
                var name = funcCall.GetProperty("name").GetString() ?? "";
                var args = funcCall.GetProperty("args");
                
                toolCalls.Add(new ToolCall
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = "function",
                    Function = new FunctionCall
                    {
                        Name = name,
                        Arguments = args.GetRawText()
                    }
                });
            }
        }
        
        var finishReason = firstCandidate.TryGetProperty("finishReason", out var fr) 
            ? fr.GetString() 
            : "STOP";
        
        return new ChatResponse
        {
            Id = Guid.NewGuid().ToString(),
            Model = model,
            Choices = new List<Choice>
            {
                new()
                {
                    Index = 0,
                    Message = new ChatMessage
                    {
                        Role = "assistant",
                        Content = responseText,
                        ToolCalls = toolCalls.Count > 0 ? toolCalls : null
                    },
                    FinishReason = toolCalls.Count > 0 ? "tool_calls" : null
                }
            }
        };
    }
    
    public void Dispose()
    {
        httpClient.Dispose();
    }
}
