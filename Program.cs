using LearnAgent.Services;
using LearnAgent.Tools;

namespace LearnAgent;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== Learn Agent - C# Version ===");
        Console.WriteLine("模块化、可扩展的 Agent 框架");
        Console.WriteLine();

        // 加载配置
        var config = new ConfigService();
        
        try
        {
            config.Validate();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"配置错误: {ex.Message}");
            return;
        }
        
        Console.WriteLine($"API地址: {config.BaseUrl ?? "默认"}");
        Console.WriteLine($"模型: {config.ModelId}");
        Console.WriteLine();

        // 创建工具注册表
        var toolRegistry = new ToolRegistry()
            .Register(new BashTool())
            .Register(new ReadFileTool())
            .Register(new WriteFileTool());
        
        // 创建客户端
        using var client = ClientFactory.Create(config);
        
        // 创建 Agent 服务
        var agent = new AgentService(client, toolRegistry, config.ModelId, config.SystemPrompt);
        
        // 交互式循环
        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("s01 >> ");
            Console.ResetColor();
            
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input) || input?.ToLower() is "q" or "exit" or "quit")
                break;

            if (input?.ToLower() == "clear")
            {
                agent.ClearHistory();
                Console.WriteLine("历史已清空");
                continue;
            }

            try
            {
                var response = await agent.SendMessageAsync(input!);
                Console.WriteLine(response);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"错误: {ex.Message}");
                Console.ResetColor();
            }
            
            Console.WriteLine();
        }
    }
}
