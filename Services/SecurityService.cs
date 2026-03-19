using System.Security;

namespace LearnAgent.Services;

/// <summary>
/// 安全服务：提供路径验证、命令检查等安全功能
/// </summary>
public class SecurityService
{
    private readonly string workDirectory;
    
    // 危险命令黑名单
    private static readonly string[] DangerousCommands =
    [
        "rm -rf /",
        "sudo",
        "shutdown",
        "reboot",
        "format",
        "del /",
        "rmdir /s",
        "mklink",
        "> /dev/",
        "dd if=",
        ":(){ :|:& };:",  // Fork bomb
        "chmod 777",
        "chown root",
        "net user",
        "net localgroup",
        "reg delete",
        "reg add",
        "powershell -e",  // Encoded command (often malicious)
        "certutil",
        "bitsadmin",
    ];
    
    // 危险文件扩展名
    private static readonly string[] DangerousExtensions =
    [
        ".exe",
        ".bat",
        ".cmd",
        ".ps1",
        ".vbs",
        ".js",
        ".jar",
        ".msi",
    ];
    
    // 输出最大长度
    public const int MaxOutputLength = 50000;
    
    // 命令超时时间（毫秒）
    public const int CommandTimeoutMs = 120000; // 120秒
    
    public SecurityService(string? workDirectory = null)
    {
        this.workDirectory = Path.GetFullPath(workDirectory ?? Directory.GetCurrentDirectory());
    }
    
    /// <summary>
    /// 验证路径是否在工作目录内（防止路径逃逸）
    /// </summary>
    public (bool isValid, string fullPath, string error) ValidatePath(string relativePath)
    {
        try
        {
            // 处理相对路径
            string fullPath;
            
            if (Path.IsPathRooted(relativePath))
            {
                fullPath = Path.GetFullPath(relativePath);
            }
            else
            {
                fullPath = Path.GetFullPath(Path.Combine(workDirectory, relativePath));
            }
            
            // 检查是否在工作目录内
            var workDirWithSeparator = workDirectory.EndsWith(Path.DirectorySeparatorChar) 
                ? workDirectory 
                : workDirectory + Path.DirectorySeparatorChar;
            
            if (!fullPath.StartsWith(workDirWithSeparator, StringComparison.OrdinalIgnoreCase) && 
                !fullPath.Equals(workDirectory, StringComparison.OrdinalIgnoreCase))
            {
                return (false, "", $"Path escapes workspace: {relativePath}");
            }
            
            // 检查危险文件扩展名
            var extension = Path.GetExtension(fullPath).ToLowerInvariant();
            if (DangerousExtensions.Contains(extension))
            {
                return (false, "", $"Dangerous file extension blocked: {extension}");
            }
            
            return (true, fullPath, "");
        }
        catch (Exception ex)
        {
            return (false, "", $"Invalid path: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 检查命令是否危险
    /// </summary>
    public (bool isSafe, string error) CheckCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return (false, "Empty command");
        }
        
        // 检查黑名单
        foreach (var dangerous in DangerousCommands)
        {
            if (command.Contains(dangerous, StringComparison.OrdinalIgnoreCase))
            {
                return (false, $"Dangerous command blocked: contains '{dangerous}'");
            }
        }
        
        // 检查管道到危险命令
        if (command.Contains("|") && command.Split('|').Any(part => 
            DangerousCommands.Any(d => part.Contains(d, StringComparison.OrdinalIgnoreCase))))
        {
            return (false, "Dangerous piped command blocked");
        }
        
        // 检查重定向到敏感位置
        if (command.Contains(">") || command.Contains(">>"))
        {
            var redirectPattern = new[] { ">", ">>" };
            foreach (var pattern in redirectPattern)
            {
                var index = command.IndexOf(pattern);
                if (index >= 0)
                {
                    var redirectTarget = command[(index + pattern.Length)..].Trim();
                    if (redirectTarget.StartsWith("/") || 
                        redirectTarget.StartsWith("\\") ||
                        redirectTarget.Contains(".."))
                    {
                        return (false, "Redirect to absolute or parent path blocked");
                    }
                }
            }
        }
        
        return (true, "");
    }
    
    /// <summary>
    /// 截断输出以防止内存溢出
    /// </summary>
    public static string TruncateOutput(string output, int maxLength = MaxOutputLength)
    {
        if (string.IsNullOrEmpty(output))
        {
            return "(no output)";
        }
        
        if (output.Length <= maxLength)
        {
            return output;
        }
        
        return output[..maxLength] + $"\n... (truncated, {output.Length - maxLength} more characters)";
    }
    
    /// <summary>
    /// 获取工作目录
    /// </summary>
    public string GetWorkDirectory() => workDirectory;
}
