using LearnAgent.Clients;

namespace LearnAgent.Services;

/// <summary>
/// 客户端工厂
/// </summary>
public static class ClientFactory
{
    public static ILLMClient Create(ConfigService config)
    {
        var modelId = config.ModelId.ToLower();
        var baseUrl = config.BaseUrl?.ToLower() ?? "";
        
        // 根据模型或 URL 判断客户端类型
        if (modelId.Contains("gemini") || baseUrl.Contains("generativelanguage"))
        {
            return new GeminiClient(config.ApiKey, config.ModelId, config.BaseUrl);
        }
        
        // 默认使用 OpenAI 兼容客户端
        return new OpenAIClient(config.ApiKey, config.BaseUrl);
    }
}
