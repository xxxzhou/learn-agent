using System.Text.RegularExpressions;

namespace LearnAgent.Services;

/// <summary>
/// 技能加载器 - 按需加载领域知识
/// </summary>
public class SkillLoader
{
    private readonly Dictionary<string, SkillInfo> skills = new();
    private readonly string skillsDirectory;
    
    public SkillLoader(string baseDirectory)
    {
        // 尝试从多个位置查找 skills 目录
        // 1. 直接在 baseDirectory 下
        // 2. 在 baseDirectory 的父目录（如果是从 bin 运行）
        // 3. 向上查找直到找到 skills 目录
        
        var possiblePaths = new[]
        {
            Path.Combine(baseDirectory, "skills"),
            Path.Combine(baseDirectory, "..", "skills"),
            Path.Combine(baseDirectory, "..", "..", "skills"),
            Path.Combine(baseDirectory, "..", "..", "..", "skills"),
            Path.Combine(baseDirectory, "..", "..", "..", "..", "skills"),
        };
        
        foreach (var path in possiblePaths)
        {
            var fullPath = Path.GetFullPath(path);
            if (Directory.Exists(fullPath))
            {
                skillsDirectory = fullPath;
                break;
            }
        }
        
        if (string.IsNullOrEmpty(skillsDirectory))
        {
            skillsDirectory = possiblePaths[0]; // 使用第一个作为默认值
        }
        
        LoadSkills();
    }
    
    /// <summary>
    /// 加载所有技能
    /// </summary>
    private void LoadSkills()
    {
        if (!Directory.Exists(skillsDirectory))
        {
            ConsoleLogger.Warning($"Skills directory not found: {skillsDirectory}");
            return;
        }
        
        // 递归查找所有 SKILL.md 文件
        var skillFiles = Directory.GetFiles(skillsDirectory, "SKILL.md", SearchOption.AllDirectories);
        
        foreach (var file in skillFiles)
        {
            try
            {
                var content = File.ReadAllText(file);
                var skill = ParseSkillFile(file, content);
                
                if (skill != null)
                {
                    skills[skill.Name] = skill;
                    ConsoleLogger.Info($"Loaded skill: {skill.Name}");
                }
            }
            catch (Exception ex)
            {
                ConsoleLogger.Warning($"Failed to load skill from {file}: {ex.Message}");
            }
        }
        
        ConsoleLogger.Info($"Total skills loaded: {skills.Count}");
    }
    
    /// <summary>
    /// 解析技能文件
    /// </summary>
    private SkillInfo? ParseSkillFile(string filePath, string content)
    {
        // 解析 YAML frontmatter
        var frontmatterMatch = Regex.Match(content, @"^---\s*\n(.*?)\n---", RegexOptions.Singleline);
        
        if (!frontmatterMatch.Success)
        {
            return null;
        }
        
        var frontmatter = frontmatterMatch.Groups[1].Value;
        var body = content.Substring(frontmatterMatch.Length).Trim();
        
        // 解析 name 和 description
        var nameMatch = Regex.Match(frontmatter, @"name:\s*(.+?)(?:\n|$)");
        var descMatch = Regex.Match(frontmatter, @"description:\s*(.+?)(?:\n|$)");
        
        if (!nameMatch.Success)
        {
            // 使用目录名作为技能名
            var dirName = new DirectoryInfo(Path.GetDirectoryName(filePath)!).Name;
            nameMatch = Regex.Match(dirName, @"(.*)");
        }
        
        return new SkillInfo
        {
            Name = nameMatch.Success ? nameMatch.Groups[1].Value.Trim() : "unknown",
            Description = descMatch.Success ? descMatch.Groups[1].Value.Trim() : "",
            Content = body,
            FilePath = filePath
        };
    }
    
    /// <summary>
    /// 获取所有技能的描述列表（用于系统提示）
    /// </summary>
    public string GetSkillDescriptions()
    {
        if (skills.Count == 0)
        {
            return "  (no skills available)";
        }
        
        var lines = skills.Values
            .Select(s => $"  - {s.Name}: {s.Description}")
            .ToList();
        
        return string.Join("\n", lines);
    }
    
    /// <summary>
    /// 获取技能内容（用于 tool_result）
    /// </summary>
    public string GetSkillContent(string name)
    {
        if (!skills.TryGetValue(name, out var skill))
        {
            return $"Error: Unknown skill '{name}'. Available skills: {string.Join(", ", skills.Keys)}";
        }
        
        return $"<skill name=\"{skill.Name}\">\n{skill.Content}\n</skill>";
    }
    
    /// <summary>
    /// 检查技能是否存在
    /// </summary>
    public bool HasSkill(string name)
    {
        return skills.ContainsKey(name);
    }
    
    /// <summary>
    /// 获取所有技能名称
    /// </summary>
    public IEnumerable<string> GetSkillNames()
    {
        return skills.Keys;
    }
}

/// <summary>
/// 技能信息
/// </summary>
public class SkillInfo
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Content { get; set; } = "";
    public string FilePath { get; set; } = "";
}
