using System.Text.Json;

namespace LearnAgent.Tools;

/// <summary>
/// 文件读取工具
/// </summary>
public class ReadFileTool : ITool
{
    public string Name => "read_file";
    
    public string Description => 
        "Read the contents of a file and return it as text. " +
        "Parameters: file_path (string) - the path to the file to read. " +
        "Example: read_file with file_path='config.txt' returns the file contents.";
    
    public Task<string> ExecuteAsync(string argumentsJson)
    {
        var args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(argumentsJson);
        var filePath = args?.GetValueOrDefault("file_path").GetString() ?? "";
        
        if (string.IsNullOrEmpty(filePath))
        {
            return Task.FromResult("Error: file_path is required");
        }
        
        try
        {
            if (!File.Exists(filePath))
            {
                return Task.FromResult($"Error: File not found: {filePath}");
            }
            
            var content = File.ReadAllText(filePath);
            return Task.FromResult(content.Length > 50000 ? content[..50000] + "\n... (truncated)" : content);
        }
        catch (Exception ex)
        {
            return Task.FromResult($"Error: {ex.Message}");
        }
    }
}
