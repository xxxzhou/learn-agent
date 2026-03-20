namespace LearnAgent.Models;

/// <summary>
/// 用户状态常量
/// </summary>
public static class UserStatus
{
    public const string Active = "active";
    public const string Inactive = "inactive";
    public const string Suspended = "suspended";
    public const string Deleted = "deleted";

    public static bool IsValid(string status) =>
        status == Active || status == Inactive || status == Suspended || status == Deleted;
}

/// <summary>
/// 用户角色常量
/// </summary>
public static class UserRole
{
    public const string Admin = "admin";
    public const string User = "user";
    public const string Guest = "guest";

    public static bool IsValid(string role) =>
        role == Admin || role == User || role == Guest;
}

/// <summary>
/// 用户模型 - 包括字段定义、验证逻辑和数据库映射
/// </summary>
public class User
{
    /// <summary>
    /// 用户ID - 主键
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 用户名 - 必填，长度3-50字符
    /// </summary>
    public string Username { get; set; } = "";

    /// <summary>
    /// 邮箱地址 - 必填，格式验证
    /// </summary>
    public string Email { get; set; } = "";

    /// <summary>
    /// 密码哈希 - 不存储明文密码
    /// </summary>
    public string PasswordHash { get; set; } = "";

    /// <summary>
    /// 用户角色
    /// </summary>
    public string Role { get; set; } = UserRole.User;

    /// <summary>
    /// 用户状态
    /// </summary>
    public string Status { get; set; } = UserStatus.Active;

    /// <summary>
    /// 显示名称
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// 头像URL
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 最后登录时间
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// 验证用户数据
    /// </summary>
    /// <returns>验证结果，包含错误信息列表</returns>
    public (bool IsValid, List<string> Errors) Validate()
    {
        var errors = new List<string>();

        // 验证用户名
        if (string.IsNullOrWhiteSpace(Username))
        {
            errors.Add("用户名不能为空");
        }
        else if (Username.Length < 3 || Username.Length > 50)
        {
            errors.Add("用户名长度必须在3-50个字符之间");
        }

        // 验证邮箱
        if (string.IsNullOrWhiteSpace(Email))
        {
            errors.Add("邮箱不能为空");
        }
        else if (!IsValidEmail(Email))
        {
            errors.Add("邮箱格式不正确");
        }

        // 验证密码哈希
        if (string.IsNullOrWhiteSpace(PasswordHash))
        {
            errors.Add("密码哈希不能为空");
        }

        // 验证角色
        if (!UserRole.IsValid(Role))
        {
            errors.Add($"角色 '{Role}' 无效");
        }

        // 验证状态
        if (!UserStatus.IsValid(Status))
        {
            errors.Add($"状态 '{Status}' 无效");
        }

        return (errors.Count == 0, errors);
    }

    /// <summary>
    /// 验证邮箱格式
    /// </summary>
    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 检查用户是否活跃
    /// </summary>
    public bool IsActive()
    {
        return Status == UserStatus.Active;
    }

    /// <summary>
    /// 设置密码哈希
    /// </summary>
    public void SetPasswordHash(string passwordHash)
    {
        PasswordHash = passwordHash;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 更新用户信息
    /// </summary>
    public void Update(string? displayName = null, string? avatarUrl = null)
    {
        if (displayName != null)
        {
            DisplayName = displayName;
        }
        if (avatarUrl != null)
        {
            AvatarUrl = avatarUrl;
        }
        UpdatedAt = DateTime.UtcNow;
    }
}