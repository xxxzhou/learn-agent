# Learn Agent - C# 模块化版本

基于 learn-claude-code 的模块化 Agent 框架，支持动态扩展多模型、多工具。

## 项目结构

```
learn-agent/
├── Models/              # 数据模型
│   ├── ChatMessage.cs       - 聊天消息
│   ├── ChatRequest.cs       - 请求体
│   ├── ChatResponse.cs      - 响应体
│   ├── ToolDefinition.cs    - 工具定义
│   └── ToolCall.cs          - 工具调用
│
├── Clients/             # 大模型客户端
│   ├── ILLMClient.cs        - 客户端接口
│   ├── OpenAIClient.cs      - OpenAI 兼容客户端
│   └── AnthropicClient.cs   - Anthropic 客户端 (预留)
│
├── Tools/               # 工具
│   ├── ITool.cs             - 工具接口
│   ├── BashTool.cs          - Shell 命令执行
│   ├── ReadFileTool.cs      - 文件读取
│   ├── WriteFileTool.cs     - 文件写入
│   └── ToolRegistry.cs      - 工具注册表
│
├── Services/            # 服务层
│   ├── AgentService.cs      - Agent 核心逻辑
│   ├── ConfigService.cs     - 配置管理
│   └── ClientFactory.cs     - 客户端工厂
│
└── Program.cs           # 入口
```

## 快速开始

### 1. 配置

复制 `.env.example` 为 `.env`：

```bash
ANTHROPIC_BASE_URL=https://api.siliconflow.cn/v1
MODEL_ID=deepseek-ai/DeepSeek-V3
ANTHROPIC_API_KEY=你的密钥
```

### 2. 运行

```bash
dotnet run
```

## 扩展指南

### 添加新工具

1. 在 `Tools/` 创建新工具类：

```csharp
public class MyTool : ITool
{
    public string Name => "my_tool";
    public string Description => "工具描述";
    
    public Task<string> ExecuteAsync(string argumentsJson)
    {
        // 实现工具逻辑
        return Task.FromResult("结果");
    }
}
```

2. 在 `Program.cs` 注册：

```csharp
toolRegistry.Register(new MyTool());
```

### 添加新模型客户端

1. 在 `Clients/` 创建新客户端：

```csharp
public class GeminiClient : ILLMClient
{
    public string Name => "Google Gemini";
    
    public async Task<ChatResponse> ChatAsync(ChatRequest request)
    {
        // 实现 API 调用
    }
}
```

2. 在 `ClientFactory.cs` 添加创建逻辑：

```csharp
public static ILLMClient Create(ConfigService config)
{
    if (config.ModelId.Contains("gemini"))
        return new GeminiClient(config.ApiKey);
    
    return new OpenAIClient(config.ApiKey, config.BaseUrl);
}
```

## 内置工具

| 工具 | 描述 |
|------|------|
| `bash` | 执行 shell 命令 |
| `read_file` | 读取文件内容 |
| `write_file` | 写入文件 |

## 支持的模型

- DeepSeek V3 / R1 (硅基流动)
- Qwen 系列 (硅基流动)
- 其他 OpenAI 兼容 API

## 命令

- `clear` - 清空对话历史
- `q` / `exit` - 退出程序
