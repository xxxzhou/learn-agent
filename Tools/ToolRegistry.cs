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
            "file_task" => new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["action"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Action to perform: create, get, update, list, claim, unclaim, delete"
                    },
                    ["task_id"] = new Dictionary<string, object>
                    {
                        ["type"] = "integer",
                        ["description"] = "Task ID (required for get, update, claim, unclaim, delete)"
                    },
                    ["subject"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Task subject/title (required for create)"
                    },
                    ["description"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Task description (optional for create/update)"
                    },
                    ["status"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Task status: pending, in_progress, completed (for update)",
                        ["enum"] = new[] { "pending", "in_progress", "completed" }
                    },
                    ["owner"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Owner name (required for claim)"
                    },
                    ["add_blocked_by"] = new Dictionary<string, object>
                    {
                        ["type"] = "array",
                        ["items"] = new Dictionary<string, object> { ["type"] = "integer" },
                        ["description"] = "Task IDs that this task depends on (for update)"
                    },
                    ["add_blocks"] = new Dictionary<string, object>
                    {
                        ["type"] = "array",
                        ["items"] = new Dictionary<string, object> { ["type"] = "integer" },
                        ["description"] = "Task IDs that this task blocks (for update)"
                    }
                },
                ["required"] = new List<string> { "action" }
            },
            "background_run" => new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["command"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "The shell command to run in the background (non-blocking)"
                    }
                },
                ["required"] = new List<string> { "command" }
            },
            "background_check" => new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["task_id"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "The background task ID to check"
                    }
                },
                ["required"] = new List<string> { "task_id" }
            },
            "background_list" => new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>()
            },
            "teammate_spawn" => new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["name"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Unique name for the teammate"
                    },
                    ["role"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Role description (e.g., 'coder', 'tester', 'reviewer')"
                    },
                    ["prompt"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Initial task or instruction for the teammate"
                    },
                    ["mode"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Permission mode: default, acceptEdits, bypassPermissions, plan"
                    }
                },
                ["required"] = new List<string> { "name", "role" }
            },
            "send_message" => new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["recipient"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Name of the teammate to send message to"
                    },
                    ["content"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Message content"
                    },
                    ["sender"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Sender name (default: 'lead')"
                    }
                },
                ["required"] = new List<string> { "recipient", "content" }
            },
            "broadcast" => new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["content"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Message content to broadcast to all teammates"
                    },
                    ["summary"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Short summary of the message"
                    }
                },
                ["required"] = new List<string> { "content" }
            },
            "read_inbox" => new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["name"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Name to check inbox for (default: 'lead' for leader)"
                    }
                },
                ["required"] = new List<string>()
            },
            "team_status" => new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>()
            },
            "teammate_shutdown" => new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["name"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Name of the teammate to shutdown"
                    }
                },
                ["required"] = new List<string> { "name" }
            },
            "shutdown_request" => new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["recipient"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Name of the teammate to request shutdown"
                    },
                    ["reason"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Reason for shutdown request"
                    }
                },
                ["required"] = new List<string> { "recipient" }
            },
            "shutdown_response" => new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["request_id"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "The request ID to respond to"
                    },
                    ["approve"] = new Dictionary<string, object>
                    {
                        ["type"] = "boolean",
                        ["description"] = "Whether to approve the shutdown"
                    },
                    ["reason"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Optional reason for the response"
                    }
                },
                ["required"] = new List<string> { "request_id", "approve" }
            },
            "plan_submit" => new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["recipient"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Name of the teammate to submit plan to (default: 'lead')"
                    },
                    ["plan"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "The plan content to submit"
                    },
                    ["summary"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Short summary of the plan"
                    }
                },
                ["required"] = new List<string> { "plan" }
            },
            "plan_review" => new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["request_id"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "The request ID to review"
                    },
                    ["approve"] = new Dictionary<string, object>
                    {
                        ["type"] = "boolean",
                        ["description"] = "Whether to approve the plan"
                    },
                    ["feedback"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Feedback for the plan"
                    }
                },
                ["required"] = new List<string> { "request_id", "approve" }
            },
            // S11: 自治工具
            "idle" => new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>()
            },
            "claim_task" => new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["task_id"] = new Dictionary<string, object>
                    {
                        ["type"] = "integer",
                        ["description"] = "The ID of the task to claim"
                    }
                },
                ["required"] = new List<string> { "task_id" }
            },
            "scan_tasks" => new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>()
            },
            // S12: Worktree 工具
            "worktree_create" => new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["name"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Unique name for the worktree"
                    },
                    ["task_id"] = new Dictionary<string, object>
                    {
                        ["type"] = "integer",
                        ["description"] = "Task ID to bind (auto-sets status to in_progress)"
                    },
                    ["base_branch"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Base branch for the new worktree branch"
                    }
                },
                ["required"] = new List<string> { "name" }
            },
            "worktree_exec" => new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["name"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Worktree name"
                    },
                    ["command"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Command to execute in worktree directory"
                    }
                },
                ["required"] = new List<string> { "name", "command" }
            },
            "worktree_keep" => new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["name"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Worktree name to mark as kept"
                    }
                },
                ["required"] = new List<string> { "name" }
            },
            "worktree_remove" => new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["name"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Worktree name to remove"
                    },
                    ["force"] = new Dictionary<string, object>
                    {
                        ["type"] = "boolean",
                        ["description"] = "Force removal"
                    },
                    ["complete_task"] = new Dictionary<string, object>
                    {
                        ["type"] = "boolean",
                        ["description"] = "Mark bound task as completed"
                    }
                },
                ["required"] = new List<string> { "name" }
            },
            "worktree_list" => new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>()
            },
            "worktree_bind" => new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["task_id"] = new Dictionary<string, object>
                    {
                        ["type"] = "integer",
                        ["description"] = "Task ID to bind"
                    },
                    ["worktree"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Worktree name to bind to"
                    }
                },
                ["required"] = new List<string> { "task_id", "worktree" }
            },
            "worktree_events" => new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["limit"] = new Dictionary<string, object>
                    {
                        ["type"] = "integer",
                        ["description"] = "Max events to show (default 20)"
                    }
                }
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
