namespace LearnAgent.Services;

/// <summary>
/// 配置服务
/// </summary>
public class ConfigService
{
    public string ApiKey { get; private set; } = "";
    public string ModelId { get; private set; } = "";
    public string? BaseUrl { get; private set; }
    public string? SystemPrompt { get; private set; }
    
    public ConfigService()
    {
        LoadFromEnvFile();
        LoadFromEnvironment();
        
        ApiKey = ApiKey.Trim();
    }
    
    private void LoadFromEnvFile()
    {
        var envFile = Path.Combine(Directory.GetCurrentDirectory(), ".env");
        if (!File.Exists(envFile)) return;
        
        foreach (var line in File.ReadAllLines(envFile))
        {
            var trimmedLine = line.Trim();
            
            if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#"))
                continue;
            
            var equalIndex = trimmedLine.IndexOf('=');
            if (equalIndex <= 0) continue;
            
            var key = trimmedLine.Substring(0, equalIndex).Trim();
            var value = trimmedLine.Substring(equalIndex + 1).Trim();
            
            if (Environment.GetEnvironmentVariable(key) == null)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
    
    private void LoadFromEnvironment()
    {
        // 读取 API Key（支持多种环境变量名）
        ApiKey = Environment.GetEnvironmentVariable("API_KEY")
            ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") 
            ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY") 
            ?? "";
        
        ModelId = Environment.GetEnvironmentVariable("MODEL_ID") ?? "glm-4";
        
        // 读取 Base URL（支持多种环境变量名）
        BaseUrl = Environment.GetEnvironmentVariable("API_BASE_URL")
            ?? Environment.GetEnvironmentVariable("ANTHROPIC_BASE_URL") 
            ?? Environment.GetEnvironmentVariable("OPENAI_BASE_URL");
        
        SystemPrompt = @"You have access to these tools. You MUST call them directly when needed:

1. bash - Execute shell commands
   - Use 'dir' on Windows to list files
   - Example: to list files, call bash with command='dir'

2. read_file - Read file contents
   - Example: call read_file with file_path='filename.txt'

3. write_file - Write to files
   - Example: call write_file with file_path='test.txt' and content='hello'

IMPORTANT: Do NOT ask the user for commands. When they ask to list files, YOU call bash directly with command='dir'. When they ask to read a file, YOU call read_file directly.

Always take action using tools immediately. Do not explain what you would do - just do it.

用中文回复。";
    }
    
    public void Validate()
    {
        if (string.IsNullOrEmpty(ApiKey))
        {
            throw new InvalidOperationException(
                "请设置 API Key。方式1: 在 .env 文件中设置 ANTHROPIC_API_KEY。方式2: 设置环境变量。");
        }
        
        if (ApiKey.Any(c => c > 127))
        {
            throw new InvalidOperationException(
                "API Key 包含非 ASCII 字符，请检查 .env 文件编码或手动设置环境变量。");
        }
    }
}
