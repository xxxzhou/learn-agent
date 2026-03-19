using LearnAgent.Models;

namespace LearnAgent.Clients;

/// <summary>
/// 大模型客户端接口
/// </summary>
public interface ILLMClient : IDisposable
{
    /// <summary>
    /// 客户端名称
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// 发送聊天请求
    /// </summary>
    Task<ChatResponse> ChatAsync(ChatRequest request);
}
