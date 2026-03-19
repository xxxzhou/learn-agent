using System.Text.Json;
using LearnAgent.Services;

namespace LearnAgent.Tools;

/// <summary>
/// 文件写入工具
/// </summary>
public class WriteFileTool : ITool
{
    public string Name => "write_file";
    
    public string Description => 
        "Write text content to a file safely. " +
        "Parameters: file_path (string) - the path (relative to workspace), " +
        "content (string) - the text to write. " +
        "Path escaping (../) and dangerous extensions (.exe, .bat, etc.) are blocked.";
    
    private readonly SecurityService security;
    
    public WriteFileTool(SecurityService security)
    {
        this.security = security;
    }
    
    public Task<string> ExecuteAsync(string argumentsJson)
    {
        try
        {
            var args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(argumentsJson);
            
            string filePath = "";
            string content = "";
            
            if (args != null)
            {
                if (args.TryGetValue("file_path", out var pathElement))
                {
                    filePath = pathElement.GetString() ?? "";
                }
                if (args.TryGetValue("content", out var contentElement))
                {
                    content = contentElement.GetString() ?? "";
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
            
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            File.WriteAllText(fullPath, content);
            return Task.FromResult($"Successfully wrote {content.Length} characters to {filePath}");
        }
        catch (Exception ex)
        {
            return Task.FromResult($"Error: {ex.Message}");
        }
    }
}
