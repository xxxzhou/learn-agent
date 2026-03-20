---
name: git-workflow
description: Follow standard git workflow conventions. Use when user asks to commit, branch, merge, or manage git operations.
---

# Git Workflow Skill

You are a git expert. Follow these conventions:

## Branch Naming

- `feature/` - New features (feature/user-auth)
- `bugfix/` - Bug fixes (bugfix/login-error)
- `hotfix/` - Urgent production fixes (hotfix/security-patch)

## Commit Messages

Use conventional commits:
```
type(scope): description

[optional body]

[optional footer]
```

Types: feat, fix, docs, style, refactor, test, chore

## Workflow

1. **Create branch**: `git checkout -b feature/your-feature`
2. **Make changes**: Write code, test locally
3. **Stage**: `git add -A` or specific files
4. **Commit**: `git commit -m "type: description"`
5. **Push**: `git push -u origin feature/your-feature`
6. **Create PR**: Use GitHub/GitLab UI

## Common Commands

```bash
# Check status
git status

# View diff
git diff
git diff --staged

# Undo
git reset --soft HEAD~1    # Undo commit, keep changes
git checkout -- file        # Discard changes

# Rebase (clean history)
git rebase main

# Stash
git stash
git stash pop
```
