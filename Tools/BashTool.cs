using System.Diagnostics;
using System.Text.Json;

namespace LearnAgent.Tools;

/// <summary>
/// Bash 命令执行工具
/// </summary>
public class BashTool : ITool
{
    public string Name => "bash";
    
    public string Description => 
        "Execute a shell command. " +
        "Parameters: command (string) - the shell command to run. " +
        "Use 'dir' on Windows to list files, 'type filename' to read file, 'copy' to copy files. " +
        "Dangerous commands are blocked.";
    
    private static readonly string[] DangerousCommands = 
    { 
        "rm -rf", "sudo", "shutdown", "reboot", "format", "del /"
    };
    
    public Task<string> ExecuteAsync(string argumentsJson)
    {
        var args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(argumentsJson);
        var command = args?.GetValueOrDefault("command").GetString() ?? "";
        
        if (DangerousCommands.Any(d => command.Contains(d, StringComparison.OrdinalIgnoreCase)))
        {
            return Task.FromResult("Error: Dangerous command blocked");
        }
        
        return Task.FromResult(RunCommand(command));
    }
    
    private static string RunCommand(string command)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {command}",
                WorkingDirectory = Directory.GetCurrentDirectory(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            using var process = Process.Start(psi);
            if (process == null) return "Error: Failed to start process";
            
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            
            process.WaitForExit(120000);
            
            var result = (output + error).Trim();
            return result.Length > 50000 ? result[..50000] : (result ?? "(no output)");
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}
