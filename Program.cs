using System.Runtime.InteropServices;
using System.Text;
using LearnAgent.Services;
using LearnAgent.Tools;

namespace LearnAgent;

public class Program
{
    // Windows API 声明
    private const int STD_OUTPUT_HANDLE = -11;
    private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
    private const uint ENABLE_PROCESSED_OUTPUT = 0x0001;
    
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetStdHandle(int nStdHandle);
    
    [DllImport("kernel32.dll")]
    private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);
    
    public static async Task Main(string[] args)
    {
        // 设置控制台编码为 UTF-8，解决中文乱码问题
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;
        
        // Windows 控制台特殊处理：启用 ANSI 转义序列支持
        if (OperatingSystem.IsWindows())
        {
            var handle = GetStdHandle(STD_OUTPUT_HANDLE);
            SetConsoleMode(handle, ENABLE_VIRTUAL_TERMINAL_PROCESSING | ENABLE_PROCESSED_OUTPUT);
        }
        
        Console.WriteLine("=== Learn Agent - s03 Todo Write ===");
        Console.WriteLine("任务规划与进度跟踪");
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
        
        // 创建工具注册表（注入依赖）
        var toolRegistry = new ToolRegistry()
            .Register(new BashTool(security))
            .Register(new ReadFileTool(security))
            .Register(new WriteFileTool(security))
            .Register(new TodoTool(todoManager));
        
        // 创建客户端
        using var client = ClientFactory.Create(config);
        
        // 更新系统提示，鼓励使用 todo 工具
        var systemPrompt = config.SystemPrompt + 
            " Use the todo tool to plan and track progress on multi-step tasks. " +
            "Mark tasks as in_progress before starting, and completed when done.";
        
        // 创建 Agent 服务
        var agent = new AgentService(client, toolRegistry, config.ModelId, systemPrompt);
        
        // 交互式循环
        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("s03 >> ");
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
