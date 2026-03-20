using LearnAgent.Models;

namespace LearnAgent.Tools;

/// <summary>
/// 工具注册表
/// </summary>
public class ToolRegistry
{
    private readonly Dictionary<string, ITool> tools = new();
    
    public ToolRegistry Register(ITool tool)
    {
        tools[tool.Name] = tool;
        return this;
    }
    
    public ITool? Get(string name)
    {
        return tools.GetValueOrDefault(name);
    }
    
    public List<ToolDefinition> GetToolDefinitions()
    {
        var definitions = tools.Values.Select(t => new ToolDefinition
        {
            Type = "function",
            Function = new FunctionDefinition
            {
                Name = t.Name,
                Description = t.Description,
                Parameters = GetParametersForTool(t.Name)
            }
        }).ToList();

        // 手动添加 compact 工具（由 AgentService 处理，不在 tools 字典中）
        definitions.Add(new ToolDefinition
        {
            Type = "function",
            Function = new FunctionDefinition
            {
                Name = "compact",
                Description = "Manually trigger context compression to reduce token usage",
                Parameters = GetParametersForTool("compact")
            }
        });

        return definitions;
    }
    
    private Dictionary<string, object> GetParametersForTool(string toolName)
    {
        return toolName switch
        {
            "bash" => new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["command"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "The shell command to execute"
                    }
                },
                ["required"] = new List<string> { "command" }
            },
            "read_file" => new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["file_path"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "The path to the file to read"
                    }
                },
                ["required"] = new List<string> { "file_path" }
            },
            "write_file" => new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["file_path"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "The path to the file to write"
                    },
                    ["content"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "The content to write to the file"
                    }
                },
                ["required"] = new List<string> { "file_path", "content" }
            },
            "load_skill" => new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["name"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "The name of the skill to load (e.g., 'code-review', 'git-workflow')"
                    }
                },
                ["required"] = new List<string> { "name" }
            },
            "task" => new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["prompt"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "The task description for the subagent"
                    },
                    ["description"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Short description of the task"
                    }
                },
                ["required"] = new List<string> { "prompt" }
            },
            "compact" => new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["reason"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Optional reason for compacting the context"
                    }
                },
                ["required"] = new List<string>()
            },
            _ => new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>()
            }
        };
    }
    
    public async Task<string> ExecuteAsync(string toolName, string argumentsJson)
    {
        var tool = Get(toolName);
        if (tool == null)
        {
            return $"Error: Tool '{toolName}' not found";
        }
        
        return await tool.ExecuteAsync(argumentsJson);
    }
}
