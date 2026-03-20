---
name: code-review
description: Perform thorough code reviews with security, performance, and maintainability analysis. Use when user asks to review code, check for bugs, or audit a codebase.
---

# Code Review Skill

You now have expertise in conducting comprehensive code reviews. Follow this structured approach:

## Review Checklist

### 1. Security (Critical)

Check for:
- **Injection vulnerabilities**: SQL, command, XSS, template injection
- **Authentication issues**: Hardcoded credentials, weak auth
- **Authorization flaws**: Missing access controls, IDOR
- **Data exposure**: Sensitive data in logs, error messages

### 2. Correctness

Check for:
- **Logic errors**: Off-by-one, null handling, edge cases
- **Race conditions**: Concurrent access without synchronization
- **Resource leaks**: Unclosed files, connections, memory
- **Error handling**: Swallowed exceptions, missing error paths

### 3. Performance

Check for:
- **N+1 queries**: Database calls in loops
- **Memory issues**: Large allocations
- **Blocking operations**: Sync I/O in async code

### 4. Maintainability

Check for:
- **Naming**: Clear, consistent, descriptive
- **Complexity**: Functions > 50 lines, deep nesting > 3 levels
- **Duplication**: Copy-pasted code blocks
- **Dead code**: Unused imports, unreachable branches

## Review Workflow

1. **Understand context**: Read the code changes
2. **Run tests**: Verify existing tests pass
3. **Manual review**: Use checklist above
4. **Write feedback**: Be specific, suggest fixes, be kind
