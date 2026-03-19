using System.Text.Json;
using LearnAgent.Services;

namespace LearnAgent.Tools;

/// <summary>
/// 文件读取工具
/// </summary>
public class ReadFileTool : ITool
{
    public string Name => "read_file";
    
    public string Description => 
        "Read the contents of a file safely. " +
        "Parameters: file_path (string) - the path to the file (relative to workspace), " +
        "limit (optional int) - max lines to read. " +
        "Path escaping (../) is blocked. Output truncated at 50000 characters.";
    
    private readonly SecurityService security;
    
    public ReadFileTool(SecurityService security)
    {
        this.security = security;
    }
    
    public Task<string> ExecuteAsync(string argumentsJson)
    {
        try
        {
            var args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(argumentsJson);
            
            string filePath = "";
            int limit = 0;
            
            if (args != null)
            {
                if (args.TryGetValue("file_path", out var pathElement))
                {
                    filePath = pathElement.GetString() ?? "";
                }
                if (args.TryGetValue("limit", out var limitElement))
                {
                    limit = limitElement.GetInt32();
                }
            }
        
            if (string.IsNullOrEmpty(filePath))
            {
                return Task.FromResult("Error: file_path is required");
            }
            
            // 路径安全检查
            var (isValid, fullPath, error) = security.ValidatePath(filePath);
            if (!isValid)
            {
                return Task.FromResult($"Error: {error}");
            }
            
            if (!File.Exists(fullPath))
            {
                return Task.FromResult($"Error: File not found: {fullPath}");
            }
            
            var content = File.ReadAllText(fullPath);
            
            // 行数限制
            if (limit > 0)
            {
                var lines = content.Split('\n');
                if (lines.Length > limit)
                {
                    content = string.Join('\n', lines.Take(limit)) + 
                              $"\n... ({lines.Length - limit} more lines)";
                }
            }
            
            // 输出截断
            return Task.FromResult(SecurityService.TruncateOutput(content));
        }
        catch (Exception ex)
        {
            return Task.FromResult($"Error: {ex.Message}");
        }
    }
}
