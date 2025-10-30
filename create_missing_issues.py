#!/usr/bin/env python3

import subprocess
import json

# Missing tasks that need to be created
missing_tasks = [
    # Epic 1 - Control Plane Foundations  
    {"id": "E1-T2", "title": "Integrate Microsoft Agent Framework SDK", "description": "Configure agent runtime base classes and tool registry integration.", "epic": "Epic 1 – Control Plane Foundations", "labels": "task,epic-1,control-plane,api"},
    {"id": "E1-T3", "title": "Database setup", "description": "Create Postgres schema for agents, versions, deployments, nodes, runs.", "epic": "Epic 1 – Control Plane Foundations", "labels": "task,epic-1,control-plane,api"},
    {"id": "E1-T4", "title": "Redis integration", "description": "Implement lease and lock store with TTL expiry.", "epic": "Epic 1 – Control Plane Foundations", "labels": "task,epic-1,control-plane,api"},
    {"id": "E1-T5", "title": "NATS setup", "description": "Provision JetStream topics and publish test events.", "epic": "Epic 1 – Control Plane Foundations", "labels": "task,epic-1,control-plane,api"},
    {"id": "E1-T6", "title": "gRPC service contract", "description": "Implement LeaseService (Pull, Ack, Complete, Fail) for node communication.", "epic": "Epic 1 – Control Plane Foundations", "labels": "task,epic-1,control-plane,api"},
    {"id": "E1-T7", "title": "Scheduler service", "description": "Create least-loaded scheduling strategy with region constraint.", "epic": "Epic 1 – Control Plane Foundations", "labels": "task,epic-1,control-plane,api"},
    {"id": "E1-T8", "title": "OpenTelemetry wiring", "description": "Add tracing, metrics, and logging through OTel Collector.", "epic": "Epic 1 – Control Plane Foundations", "labels": "task,epic-1,control-plane,api"},
    {"id": "E1-T9", "title": "Authentication setup", "description": "Integrate Keycloak OIDC middleware for development.", "epic": "Epic 1 – Control Plane Foundations", "labels": "task,epic-1,control-plane,api"},
    {"id": "E1-T10", "title": "Containerization", "description": "Dockerize services and build Helm chart for control plane.", "epic": "Epic 1 – Control Plane Foundations", "labels": "task,epic-1,control-plane,api"},
    {"id": "E1-T11", "title": "CI pipeline", "description": "Build/test pipeline with SBOM generation and image signing.", "epic": "Epic 1 – Control Plane Foundations", "labels": "task,epic-1,control-plane,api"},
    
    # Missing T1 tasks from other epics
    {"id": "E2-T1", "title": "Node runtime skeleton", "description": "Create .NET Worker Service with gRPC client implementation.", "epic": "Epic 2 – Node Runtime & Connectors", "labels": "task,epic-2,node-runtime,connectors"},
    {"id": "E3-T1", "title": "AgentDefinition model", "description": "Implement CRUD API for agent definitions.", "epic": "Epic 3 – Agent Definition & Deployment Flow", "labels": "task,epic-3,agent-definition,deployment"},
    {"id": "E4-T1", "title": "OTel Collector deployment", "description": "Deploy and configure OTel Collector with Prometheus, Tempo, Loki exporters.", "epic": "Epic 4 – Observability Stack", "labels": "task,epic-4,observability,monitoring"},
    {"id": "E5-T1", "title": "Next.js setup", "description": "Initialize UI project with Tailwind and shadcn/ui.", "epic": "Epic 5 – Admin UI (Minimal)", "labels": "task,epic-5,ui,frontend"},
    {"id": "E6-T1", "title": "Local environment", "description": "k3d setup script for all core services.", "epic": "Epic 6 – Infrastructure & CI/CD", "labels": "task,epic-6,infrastructure,ci-cd"},
    {"id": "E7-T1", "title": "Unit tests", "description": "API, scheduler, connectors.", "epic": "Epic 7 – Testing & Validation", "labels": "task,epic-7,testing,validation"},
    {"id": "E8-T1", "title": "README & Quickstart", "description": "Include Azure AI Foundry configuration.", "epic": "Epic 8 – Documentation & Demo", "labels": "task,epic-8,documentation,demo"},
]

def create_github_issue(task):
    """Create a GitHub issue using GitHub CLI"""
    title = f"[{task['id']}] {task['title']}"
    
    body = f"""## Task Description
{task['description']}

## Epic/Phase
**{task['epic']}**

## Project Context
- **Project:** Business Process Agents MVP
- **Owner:** Platform Engineering
- **Task ID:** {task['id']}

## Acceptance Criteria
- [ ] Implementation complete
- [ ] Unit tests written
- [ ] Integration tests passing
- [ ] Documentation updated
- [ ] Code reviewed and approved

---
*This issue was auto-generated from tasks.yaml*"""
    
    cmd = [
        "gh", "issue", "create",
        "--title", title,
        "--body", body,
        "--label", task['labels']
    ]
    
    try:
        result = subprocess.run(cmd, capture_output=True, text=True, check=True)
        issue_url = result.stdout.strip()
        print(f"Created issue: {title}")
        print(f"  URL: {issue_url}")
        return issue_url
    except subprocess.CalledProcessError as e:
        print(f"Error creating issue '{title}': {e}")
        if e.stderr:
            print(f"  Error output: {e.stderr}")
        return None

def main():
    print("Creating missing GitHub issues...")
    print("=" * 50)
    
    created_issues = []
    
    for task in missing_tasks:
        issue_url = create_github_issue(task)
        if issue_url:
            created_issues.append({
                'task_id': task['id'],
                'title': task['title'],
                'url': issue_url
            })
    
    print(f"\n" + "=" * 50)
    print(f"Created {len(created_issues)} missing issues")
    
    # Update the summary file
    try:
        with open('github_issues_summary.json', 'r') as f:
            summary = json.load(f)
        
        summary['created_issues'].extend(created_issues)
        
        with open('github_issues_summary.json', 'w') as f:
            json.dump(summary, f, indent=2)
        
        print("Updated github_issues_summary.json")
    except Exception as e:
        print(f"Error updating summary: {e}")

if __name__ == "__main__":
    main()