> **Status:** Accepted
>
> **Version:** 0.1
>
> **Last Updated:** 2026-07-01
>
> **Applies To:** Deadbelt Operations Platform (DOP)
>
> **Audience:** Contributors, Architects, Maintainers
>
> **Related Documents:**
> - docs/development/branching-strategy.md
> - docs/development/governance.md
> - CONTRIBUTING.md

# ADR-0006: GitHub Issues as Source of Truth

## Status

Accepted

## Context

Deadbelt Operations Platform is an open-source project intended to support long-term community contribution, public planning, and transparent decision-making.

As the project grows, work needs to be tracked in a way that is visible, reviewable, and connected to code changes. Informal notes, chat discussions, and local planning are useful during early development, but they are not sufficient as a permanent source of project history.

The project needs a consistent workflow for:

- Feature planning
- Bug tracking
- Architecture work
- Pull request linkage
- Release planning
- Contributor onboarding
- Historical traceability

## Decision

GitHub Issues will be the source of truth for planned work in DOP.

Every meaningful feature, bug fix, documentation update, architectural cleanup, or release task should begin with a GitHub Issue.

The standard workflow is:

1. Create or select an issue.
2. Sync local `main`.
3. Create a feature branch from `main`.
4. Implement the work.
5. Commit changes.
6. Push the branch.
7. Open a pull request.
8. Link the pull request to the issue.
9. Merge after validation.
10. Let GitHub close the issue automatically when possible.

Branch names should include the issue number when practical.

Example:

    feature/9-workspace-navigation-shell

Pull requests should include:

- Summary
- Completed work
- Validation performed
- Linked issue using `Closes #<issue-number>`

## Alternatives Considered

### Chat-Based Planning Only

Rejected because chat history is not a durable project management system and is not visible to future contributors.

### Project Board Only

Rejected because project boards are useful for status tracking, but individual issues provide better detail, discussion, history, and pull request linkage.

### Commit Messages Only

Rejected because commit messages explain code changes after the fact but do not provide enough context for planning, discussion, or contributor participation.

## Consequences

### Positive

- Work is transparent and traceable.
- Contributors can understand what is planned and why.
- Pull requests can be linked directly to planned work.
- GitHub milestones can group related issues into release goals.
- Project history becomes easier to audit later.

### Negative

- Small changes require some process overhead.
- Issue numbering may not be sequential by issue type because GitHub issues and pull requests share the same number sequence.
- Contributors must be disciplined about creating issues before implementation.

## Notes

GitHub issue and pull request numbers share a single sequence. For example, Issue #1, Issue #2, PR #3, PR #4, and Issue #5 is normal behavior.

To avoid confusion, DOP should refer to work by both type and number when needed:

    Issue #7 - Implement Workspace Dashboard Shell
    PR #8 - Implement Workspace Dashboard Shell