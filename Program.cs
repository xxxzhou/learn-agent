using System.Runtime.InteropServices;
using System.Text;
using LearnAgent.Services;
using LearnAgent.Tools;

namespace LearnAgent;

public class Program
{     
    public static async Task Main(string[] args)
    {
        // 设置控制台编码为 UTF-8，解决中文乱码问题
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.Unicode;
        
        Console.WriteLine("=== Learn Agent - s04 Subagent ===");
        Console.WriteLine("子代理上下文隔离");
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

        // 创建安全服务
        var security = new SecurityService();
        
        // 创建 Todo 管理器
        var todoManager = new TodoManager();
        
        // 创建工具注册表（先不注册 TaskTool）
        var toolRegistry = new ToolRegistry()
            .Register(new BashTool(security))
            .Register(new ReadFileTool(security))
            .Register(new WriteFileTool(security))
            .Register(new TodoTool(todoManager));
        
        // 创建客户端
        using var client = ClientFactory.Create(config);
        
        // 创建子代理服务
        var subagentService = new SubagentService(
            client, 
            toolRegistry, 
            config.ModelId,
            security.GetWorkDirectory());
        
        // 注册 Task 工具（需要子代理服务）
        toolRegistry.Register(new TaskTool(subagentService));
        
        // 更新系统提示，鼓励使用 task 工具委派子任务
        var systemPrompt = config.SystemPrompt + 
            "\n\nIMPORTANT INSTRUCTIONS:\n" +
            "1. Use the 'task' tool when you need to explore files, search code, or do complex analysis.\n" +
            "   Example: task(prompt=\"Search all .cs files for TODO comments\", description=\"Search TODOs\")\n" +
            "2. The task tool creates a subagent with fresh context - use it to keep your context clean.\n" +
            "3. Use the 'todo' tool to track progress on multi-step tasks.\n" +
            "4. Do NOT repeat the same action multiple times.";
        
        // 创建 Agent 服务
        var agent = new AgentService(client, toolRegistry, config.ModelId, systemPrompt);
        
        // 交互式循环
        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("s04 >> ");
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
