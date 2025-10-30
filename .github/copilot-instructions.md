# GitHub Copilot Instructions

## Project Overview

This is the **Business Process Agents MVP** project owned by **Platform Engineering**. All AI agents working on this codebase must follow these core instructions to ensure consistency, quality, and alignment with project goals.

## 📋 Core Requirements

### 1. Architecture Reference
- **ALWAYS** refer to the [System Architecture Document (SAD)](../sad.md) before making any architectural decisions
- The SAD defines the overall system design, component interactions, and technical constraints
- If your changes impact the architecture, ensure they align with the documented design patterns
- When in doubt about architectural decisions, consult the SAD first

### 2. Task Management
- Review the [tasks.yaml](../tasks.yaml) file to understand the project scope and current priorities
- All work should align with the defined epics and tasks
- Reference the appropriate task ID (e.g., E2-T3) in commit messages and pull requests
- Check GitHub issues for the most current status of tasks

## 🌿 Branching Strategy (Trunk-Based Development)

### Branch Naming Convention
```
feature/<task-id>-<short-description>
bugfix/<issue-number>-<short-description>
hotfix/<short-description>
```

**Examples:**
- `feature/E2-T3-node-registration`
- `bugfix/45-fix-memory-leak`
- `hotfix/security-patch`

### Development Workflow

1. **Create Short-Lived Feature Branches**
   - Keep branches small and focused (< 3 days of work)
   - One feature/task per branch
   - Regular commits with clear messages

2. **Frequent Integration**
   - Pull from `main` frequently to avoid conflicts
   - Push feature branches daily
   - Create draft PRs early for visibility

3. **Fast-Forward Merges**
   - Use pull requests for all changes to `main`
   - Ensure CI passes before merging
   - Delete feature branches after merge

4. **Commit Message Format**
   ```
   <type>(<task-id>): <description>
   
   <body>
   
   Refs: #<issue-number>
   ```
   
   **Types:** feat, fix, docs, style, refactor, test, chore
   
   **Example:**
   ```
   feat(E2-T3): implement node registration endpoint
   
   Add gRPC endpoint for worker nodes to register with control plane.
   Includes heartbeat mechanism and node metadata validation.
   
   Refs: #4
   ```

## 🏗️ Code Quality Standards

### 1. Code Organization
- Follow the project structure defined in the SAD
- Maintain clear separation of concerns between components
- Use consistent naming conventions across the codebase

### 2. Documentation
- Update relevant documentation when making changes
- Include inline comments for complex business logic
- Update API documentation for interface changes

### 3. Testing
- Write unit tests for new functionality
- Update integration tests when changing interfaces
- Ensure all tests pass before creating PR

### 4. Error Handling
- Implement proper error handling and logging
- Use structured logging with appropriate log levels
- Include correlation IDs for distributed tracing

## 🔧 Technical Guidelines

### 1. Technology Stack Compliance
Ensure all code aligns with the defined technology stack:
- **Control Plane**: Go with gRPC, PostgreSQL, Redis
- **Worker Nodes**: Go with Microsoft Agent Framework
- **Frontend**: React with TypeScript
- **Infrastructure**: Kubernetes, Helm, Azure services

### 2. Security Considerations
- Follow secure coding practices
- Implement proper authentication and authorization
- Use secrets management for sensitive data
- Enable mTLS for inter-service communication

### 3. Observability
- Add appropriate metrics and tracing
- Use OpenTelemetry standards
- Include health checks for all services

## 📋 Pull Request Guidelines

### PR Template Requirements
- **Title**: `[<task-id>] <clear description>`
- **Description**: Link to relevant issue and provide context
- **Testing**: Describe testing performed
- **Documentation**: Note any documentation updates needed

### Review Criteria
- Code aligns with SAD architecture
- Follows established patterns and conventions
- Includes appropriate tests
- Documentation is updated
- CI/CD pipeline passes

## 🚀 Deployment Considerations

### 1. Environment Promotion
- Changes flow through: dev → staging → production
- Use feature flags for gradual rollouts
- Monitor deployments closely

### 2. Database Changes
- Use database migrations for schema changes
- Ensure backward compatibility
- Test migrations in staging first

### 3. Configuration Management
- Use environment-specific configuration
- Externalize secrets and sensitive data
- Document configuration changes

## 📞 Escalation Process

When you encounter:
- **Architectural conflicts**: Refer to SAD or create architecture discussion issue
- **Technical blockers**: Create technical spike issue
- **Integration issues**: Coordinate with affected team members
- **Security concerns**: Flag for security review

## 🔍 Quality Checklist

Before submitting any changes, ensure:
- [ ] Code follows SAD architecture patterns
- [ ] Branch follows naming convention
- [ ] Commit messages are clear and reference task IDs
- [ ] Tests are written and passing
- [ ] Documentation is updated
- [ ] No secrets in code
- [ ] Error handling is implemented
- [ ] Logging is appropriate
- [ ] Changes are minimal and focused

## 📚 Reference Links

- [System Architecture Document](../sad.md)
- [Project Tasks](../tasks.yaml)
- [GitHub Issues](../../issues)
- [Project Repository](../../)

---

**Remember**: These instructions ensure consistency across all AI agents and human contributors. When in doubt, refer to the SAD and create issues for clarification rather than making assumptions.