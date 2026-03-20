using System.Text.Json;
using LearnAgent.Services;

namespace LearnAgent.Tools;

/// <summary>
/// 技能加载工具 - 按需加载领域知识
/// </summary>
public class LoadSkillTool : ITool
{
    private readonly SkillLoader skillLoader;
    
    public string Name => "load_skill";
    
    public string Description => "Load a skill's full content on demand. Use when you need specific domain knowledge like git workflow, code review, etc.";
    
    public LoadSkillTool(SkillLoader skillLoader)
    {
        this.skillLoader = skillLoader;
    }
    
    public async Task<string> ExecuteAsync(string argumentsJson)
    {
        try
        {
            var args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(argumentsJson);
            
            if (args == null || !args.ContainsKey("name"))
            {
                return "Error: 'name' parameter is required. Available skills: " + 
                       string.Join(", ", skillLoader.GetSkillNames());
            }
            
            var skillName = args["name"].GetString() ?? "";
            
            ConsoleLogger.Info($"Loading skill: {skillName}");
            
            var content = skillLoader.GetSkillContent(skillName);
            
            ConsoleLogger.Success($"Skill '{skillName}' loaded successfully");
            
            return content;
        }
        catch (Exception ex)
        {
            return $"Error loading skill: {ex.Message}";
        }
    }
}
