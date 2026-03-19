using System.Diagnostics;
using System.Text.Json;
using LearnAgent.Services;

namespace LearnAgent.Tools;

/// <summary>
/// Bash 命令执行工具
/// </summary>
public class BashTool : ITool
{
    public string Name => "bash";
    
    public string Description => 
        "Execute a shell command safely. " +
        "Parameters: command (string) - the shell command to run. " +
        "Dangerous commands (sudo, rm -rf, format, etc.) are blocked. " +
        "Output is truncated at 50000 characters. Timeout is 120 seconds.";
    
    private readonly SecurityService security;
    
    public BashTool(SecurityService security)
    {
        this.security = security;
    }
    
    public Task<string> ExecuteAsync(string argumentsJson)
    {
        try
        {
            var args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(argumentsJson);
            
            string command = "";
            if (args != null && args.TryGetValue("command", out var cmdElement))
            {
                command = cmdElement.GetString() ?? "";
            }
        
            // 命令安全检查
            var (isSafe, error) = security.CheckCommand(command);
            if (!isSafe)
            {
                return Task.FromResult($"Error: {error}");
            }
            
            return Task.FromResult(RunCommand(command));
        }
        catch (Exception ex)
        {
            return Task.FromResult($"Error: {ex.Message}");
        }
    }
    
    private string RunCommand(string command)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {command}",
                WorkingDirectory = security.GetWorkDirectory(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            using var process = Process.Start(psi);
            if (process == null) 
            {
                return "Error: Failed to start process";
            }
            
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            
            // 超时保护
            if (!process.WaitForExit(SecurityService.CommandTimeoutMs))
            {
                try
                {
                    process.Kill();
                }
                catch { }
                return $"Error: Command timeout ({SecurityService.CommandTimeoutMs / 1000}s)";
            }
            
            var result = (output + error).Trim();
            
            // 输出截断
            return SecurityService.TruncateOutput(result);
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}
