namespace LearnAgent.Services;

/// <summary>
/// 控制台日志工具 - 带颜色和缩进
/// </summary>
public static class ConsoleLogger
{
    // 颜色定义
    private static readonly ConsoleColor ColorDebug = ConsoleColor.DarkGray;
    private static readonly ConsoleColor ColorUser = ConsoleColor.Cyan;
    private static readonly ConsoleColor ColorAssistant = ConsoleColor.Green;
    private static readonly ConsoleColor ColorTool = ConsoleColor.Yellow;
    private static readonly ConsoleColor ColorSystem = ConsoleColor.Magenta;
    private static readonly ConsoleColor ColorInfo = ConsoleColor.White;
    private static readonly ConsoleColor ColorSuccess = ConsoleColor.Green;
    private static readonly ConsoleColor ColorError = ConsoleColor.Red;
    
    /// <summary>
    /// 打印调试信息
    /// </summary>
    public static void Debug(string message)
    {
        WriteLine("[DEBUG]", ColorDebug, message, ConsoleColor.DarkGray);
    }
    
    /// <summary>
    /// 打印用户消息
    /// </summary>
    public static void User(string message)
    {
        WriteLine("[USER]", ColorUser, message, ConsoleColor.White);
    }
    
    /// <summary>
    /// 打印助手消息
    /// </summary>
    public static void Assistant(string message)
    {
        WriteLine("[ASSISTANT]", ColorAssistant, message, ConsoleColor.White);
    }
    
    /// <summary>
    /// 打印工具调用（是大模型的响应，表示大模型决定调用此工具）
    /// </summary>
    public static void Tool(string toolName, string arguments)
    {
        // 显示这是大模型的响应
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("  ← 模型的响应: 调用工具 → ");
        Console.ForegroundColor = ColorTool;
        Console.Write(toolName);
        Console.ForegroundColor = ColorTool;
        Console.WriteLine($"({arguments})");
        Console.ResetColor();
    }

    /// <summary>
    /// 打印 API 请求开始
    /// </summary>
    public static void ApiRequest(string model, int messageCount)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("→ API请求: ");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write(model);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($" (消息数: {messageCount})");
        Console.ResetColor();
    }

    /// <summary>
    /// 打印 API 响应开始
    /// </summary>
    public static void ApiResponse()
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("← 等待API响应");
        Console.ResetColor();
    }
    
    /// <summary>
    /// 打印工具结果
    /// </summary>
    public static void ToolResult(string result, bool truncate = true)
    {
        var display = result;
        if (truncate && result.Length > 200)
        {
            display = result[..200] + "...";
        }
        
        var lines = display.Split('\n');
        foreach (var line in lines)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("  → ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(line);
        }
        Console.ResetColor();
    }
    
    /// <summary>
    /// 打印系统消息
    /// </summary>
    public static void System(string message)
    {
        WriteLine("[SYSTEM]", ColorSystem, message, ConsoleColor.White);
    }
    
    /// <summary>
    /// 打印信息
    /// </summary>
    public static void Info(string message)
    {
        WriteLine("[INFO]", ColorInfo, message, ConsoleColor.White);
    }
    
    /// <summary>
    /// 打印成功
    /// </summary>
    public static void Success(string message)
    {
        WriteLine("[OK]", ColorSuccess, message, ConsoleColor.White);
    }

    /// <summary>
    /// 打印警告
    /// </summary>
    public static void Warning(string message)
    {
        WriteLine("[WARN]", ConsoleColor.Yellow, message, ConsoleColor.White);
    }
    
    /// <summary>
    /// 打印错误
    /// </summary>
    public static void Error(string message)
    {
        WriteLine("[ERROR]", ColorError, message, ConsoleColor.White);
    }
    
    /// <summary>
    /// 打印分隔线
    /// </summary>
    public static void Separator(char c = '-', int length = 60)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(new string(c, length));
        Console.ResetColor();
    }
    
    /// <summary>
    /// 打印带标签的行
    /// </summary>
    private static void WriteLine(string label, ConsoleColor labelColor, string message, ConsoleColor messageColor)
    {
        Console.ForegroundColor = labelColor;
        Console.Write(label);
        Console.Write(" ");
        Console.ForegroundColor = messageColor;
        Console.WriteLine(message);
        Console.ResetColor();
    }
    
    /// <summary>
    /// 打印请求摘要
    /// </summary>
    public static void RequestSummary(string model, int messageCount, int showCount)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("[DEBUG] 请求: 模型=");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write(model);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write(", 消息=");
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write(messageCount);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($" (显示最后{showCount}条)");
        Console.ResetColor();
    }
    
    /// <summary>
    /// 打印消息角色和内容
    /// </summary>
    public static void Message(string role, string content, int index)
    {
        var (label, color) = role switch
        {
            "system" => ("SYS", ColorSystem),
            "user" => ("USR", ColorUser),
            "assistant" => ("AST", ColorAssistant),
            "tool" => ("TOOL", ColorTool),
            _ => ("???", ConsoleColor.Gray)
        };
        
        // 缩进索引
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write($"  [{index,2}] ");
        
        // 角色标签
        Console.ForegroundColor = color;
        Console.Write($"[{label}] ");
        
        // 内容
        Console.ForegroundColor = ConsoleColor.White;
        
        // 确保内容不是 null
        content ??= "";
        
        // 多行内容缩进处理 - 使用 StringReader 更安全
        using var reader = new System.IO.StringReader(content);
        string? line;
        bool firstLine = true;
        while ((line = reader.ReadLine()) != null)
        {
            if (!firstLine)
            {
                Console.Write(new string(' ', 10)); // 缩进对齐
            }
            firstLine = false;
            
            // 安全截断 - 按字符数而非字节数
            if (line.Length > 80)
            {
                line = line.Substring(0, 80) + "...";
            }
            Console.WriteLine(line);
        }
        
        // 如果内容为空，打印空行
        if (firstLine)
        {
            Console.WriteLine("(empty)");
        }
        
        Console.ResetColor();
    }
}
