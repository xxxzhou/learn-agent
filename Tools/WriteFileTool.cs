using System.Text.Json;

namespace LearnAgent.Tools;

/// <summary>
/// 文件写入工具
/// </summary>
public class WriteFileTool : ITool
{
    public string Name => "write_file";
    
    public string Description => 
        "Write text content to a file. Creates the file if it doesn't exist, overwrites if it does. " +
        "Parameters: file_path (string) - the path where to write, content (string) - the text to write. " +
        "Example: write_file with file_path='test.txt' and content='Hello World'.";
    
    public Task<string> ExecuteAsync(string argumentsJson)
    {
        var args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(argumentsJson);
        var filePath = args?.GetValueOrDefault("file_path").GetString() ?? "";
        var content = args?.GetValueOrDefault("content").GetString() ?? "";
        
        if (string.IsNullOrEmpty(filePath))
        {
            return Task.FromResult("Error: file_path is required");
        }
        
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            File.WriteAllText(filePath, content);
            return Task.FromResult($"Successfully wrote to {filePath}");
        }
        catch (Exception ex)
        {
            return Task.FromResult($"Error: {ex.Message}");
        }
    }
}
