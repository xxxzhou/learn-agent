using LearnAgent.Models;

namespace LearnAgent.Services;

/// <summary>
/// 任务管理器 - 管理任务列表状态
/// </summary>
public class TodoManager
{
    private readonly List<TodoItem> items = [];
    
    /// <summary>
    /// 最大任务数量
    /// </summary>
    public const int MaxItems = 20;
    
    /// <summary>
    /// 获取当前任务列表
    /// </summary>
    public IReadOnlyList<TodoItem> Items => items.AsReadOnly();
    
    /// <summary>
    /// 更新任务列表
    /// </summary>
    /// <param name="newItems">新任务列表</param>
    /// <returns>渲染后的任务列表字符串</returns>
    public (bool success, string result) Update(List<TodoItem>? newItems)
    {
        if (newItems == null || newItems.Count == 0)
        {
            items.Clear();
            return (true, "All todos cleared.");
        }
        
        // 检查数量限制
        if (newItems.Count > MaxItems)
        {
            return (false, $"Error: Maximum {MaxItems} todos allowed, got {newItems.Count}");
        }
        
        var validated = new List<TodoItem>();
        var inProgressCount = 0;
        
        for (int i = 0; i < newItems.Count; i++)
        {
            var item = newItems[i];
            
            // 验证 ID（如果没有提供，使用索引+1）
            var id = item.Id > 0 ? item.Id : i + 1;
            
            // 验证文本
            var text = item.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(text))
            {
                return (false, $"Error: Item #{id} - text is required");
            }
            
            // 验证状态
            var status = (item.Status ?? TodoStatus.Pending).ToLowerInvariant();
            if (!TodoStatus.IsValid(status))
            {
                return (false, $"Error: Item #{id} - invalid status '{item.Status}'. Valid: pending, in_progress, completed");
            }
            
            // 统计 in_progress 数量
            if (status == TodoStatus.InProgress)
            {
                inProgressCount++;
            }
            
            validated.Add(new TodoItem
            {
                Id = id,
                Text = text,
                Status = status
            });
        }
        
        // 检查同时只能有一个 in_progress
        if (inProgressCount > 1)
        {
            return (false, "Error: Only one task can be in_progress at a time");
        }
        
        // 更新列表
        items.Clear();
        items.AddRange(validated);
        
        return (true, Render());
    }
    
    /// <summary>
    /// 渲染任务列表为字符串
    /// </summary>
    public string Render()
    {
        if (items.Count == 0)
        {
            return "No todos.";
        }
        
        var lines = new List<string>();
        
        foreach (var item in items)
        {
            var marker = item.Status switch
            {
                TodoStatus.Pending => "[ ]",
                TodoStatus.InProgress => "[>]",
                TodoStatus.Completed => "[x]",
                _ => "[?]"
            };
            lines.Add($"{marker} #{item.Id}: {item.Text}");
        }
        
        // 统计完成数
        var doneCount = items.Count(t => t.Status == TodoStatus.Completed);
        lines.Add($"\n({doneCount}/{items.Count} completed)");
        
        return string.Join("\n", lines);
    }
    
    /// <summary>
    /// 清空任务列表
    /// </summary>
    public void Clear()
    {
        items.Clear();
    }
}
