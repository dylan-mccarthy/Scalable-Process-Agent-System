#!/usr/bin/env python3
"""
Script to create GitHub issues from tasks.yaml file using GitHub CLI
"""

import subprocess
import sys
import json
import re

def parse_yaml_tasks(file_path):
    """Simple YAML parser for our specific tasks.yaml structure"""
    try:
        with open(file_path, 'r') as file:
            content = file.read()
        
        tasks_data = {
            'project': '',
            'owner': '',
            'phases': [],
            'milestones': []
        }
        
        # Extract project and owner
        project_match = re.search(r'project:\s*(.+)', content)
        owner_match = re.search(r'owner:\s*(.+)', content)
        
        if project_match:
            tasks_data['project'] = project_match.group(1).strip()
        if owner_match:
            tasks_data['owner'] = owner_match.group(1).strip()
        
        # Extract phases
        phases_section = re.search(r'phases:\s*\n(.*?)(?=milestones:|$)', content, re.DOTALL)
        if phases_section:
            phases_content = phases_section.group(1)
            
            # Split by phase markers
            phase_blocks = re.split(r'\n  - name:', phases_content)
            
            for i, block in enumerate(phase_blocks):
                if i == 0:  # Skip the first empty block
                    continue
                    
                phase = {'name': '', 'goal': '', 'tasks': []}
                
                # Extract phase name and goal
                name_match = re.search(r'^([^\n]+)', block)
                goal_match = re.search(r'goal:\s*([^\n]+)', block)
                
                if name_match:
                    phase['name'] = name_match.group(1).strip()
                if goal_match:
                    phase['goal'] = goal_match.group(1).strip()
                
                # Extract tasks
                tasks_section = re.search(r'tasks:\s*\n(.*?)(?=\n  - name:|$)', block, re.DOTALL)
                if tasks_section:
                    tasks_content = tasks_section.group(1)
                    
                    # Split by task markers
                    task_blocks = re.split(r'\n      - id:', tasks_content)
                    
                    for j, task_block in enumerate(task_blocks):
                        if j == 0:  # Skip the first empty block
                            continue
                            
                        task = {'id': '', 'title': '', 'description': ''}
                        
                        # Extract task details
                        id_match = re.search(r'^([^\n]+)', task_block)
                        title_match = re.search(r'title:\s*([^\n]+)', task_block)
                        desc_match = re.search(r'description:\s*([^\n]+(?:\n[^\n]*)*?)(?=\n      - id:|$)', task_block, re.DOTALL)
                        
                        if id_match:
                            task['id'] = id_match.group(1).strip()
                        if title_match:
                            task['title'] = title_match.group(1).strip()
                        if desc_match:
                            task['description'] = desc_match.group(1).strip()
                        
                        if task['id'] and task['title']:
                            phase['tasks'].append(task)
                
                if phase['name']:
                    tasks_data['phases'].append(phase)
        
        # Extract milestones
        milestones_section = re.search(r'milestones:\s*\n(.*?)$', content, re.DOTALL)
        if milestones_section:
            milestones_content = milestones_section.group(1)
            
            # Split by milestone markers
            milestone_blocks = re.split(r'\n  - id:', milestones_content)
            
            for i, block in enumerate(milestone_blocks):
                if i == 0:  # Skip the first empty block
                    continue
                    
                milestone = {'id': '', 'title': '', 'deliverables': '', 'date': ''}
                
                # Extract milestone details
                id_match = re.search(r'^([^\n]+)', block)
                title_match = re.search(r'title:\s*([^\n]+)', block)
                deliverables_match = re.search(r'deliverables:\s*([^\n]+)', block)
                date_match = re.search(r'date:\s*([^\n]+)', block)
                
                if id_match:
                    milestone['id'] = id_match.group(1).strip()
                if title_match:
                    milestone['title'] = title_match.group(1).strip()
                if deliverables_match:
                    milestone['deliverables'] = deliverables_match.group(1).strip()
                if date_match:
                    milestone['date'] = date_match.group(1).strip()
                
                if milestone['id'] and milestone['title']:
                    tasks_data['milestones'].append(milestone)
        
        return tasks_data
        
    except Exception as e:
        print(f"Error loading tasks file: {e}")
        sys.exit(1)

def create_github_issue(task, phase_name, project_info):
    """Create a GitHub issue using GitHub CLI"""
    # Create issue title
    title = f"[{task['id']}] {task['title']}"
    
    # Create issue body with context
    body = f"""## Task Description
{task['description']}

## Epic/Phase
**{phase_name}**

## Project Context
- **Project:** {project_info['project']}
- **Owner:** {project_info['owner']}
- **Task ID:** {task['id']}

## Acceptance Criteria
- [ ] Implementation complete
- [ ] Unit tests written
- [ ] Integration tests passing
- [ ] Documentation updated
- [ ] Code reviewed and approved

---
*This issue was auto-generated from tasks.yaml*
"""
    
    # Determine labels based on epic/phase
    labels = ["task"]
    if "Epic 1" in phase_name:
        labels.extend(["epic-1", "control-plane", "api"])
    elif "Epic 2" in phase_name:
        labels.extend(["epic-2", "node-runtime", "connectors"])
    elif "Epic 3" in phase_name:
        labels.extend(["epic-3", "agent-definition", "deployment"])
    elif "Epic 4" in phase_name:
        labels.extend(["epic-4", "observability", "monitoring"])
    elif "Epic 5" in phase_name:
        labels.extend(["epic-5", "ui", "frontend"])
    elif "Epic 6" in phase_name:
        labels.extend(["epic-6", "infrastructure", "ci-cd"])
    elif "Epic 7" in phase_name:
        labels.extend(["epic-7", "testing", "validation"])
    elif "Epic 8" in phase_name:
        labels.extend(["epic-8", "documentation", "demo"])
    
    # Create the GitHub issue using gh CLI
    cmd = [
        "gh", "issue", "create",
        "--title", title,
        "--body", body,
        "--label", ",".join(labels)
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

def create_milestone_issues(milestones, project_info):
    """Create issues for milestones"""
    milestone_urls = []
    
    for milestone in milestones:
        title = f"[MILESTONE] {milestone['title']}"
        
        body = f"""## Milestone Overview
{milestone.get('description', 'Milestone for project phase completion')}

## Deliverables
{milestone['deliverables']}

## Target Date
**{milestone['date']}**

## Project Context
- **Project:** {project_info['project']}
- **Owner:** {project_info['owner']}
- **Milestone ID:** {milestone['id']}

## Completion Criteria
- [ ] All related tasks completed
- [ ] Deliverables validated
- [ ] Documentation updated
- [ ] Demo/review completed

---
*This milestone was auto-generated from tasks.yaml*
"""
        
        cmd = [
            "gh", "issue", "create",
            "--title", title,
            "--body", body,
            "--label", "milestone,epic"
        ]
        
        try:
            result = subprocess.run(cmd, capture_output=True, text=True, check=True)
            issue_url = result.stdout.strip()
            print(f"Created milestone: {title}")
            print(f"  URL: {issue_url}")
            milestone_urls.append(issue_url)
        except subprocess.CalledProcessError as e:
            print(f"Error creating milestone '{title}': {e}")
            if e.stderr:
                print(f"  Error output: {e.stderr}")
    
    return milestone_urls

def main():
    # Load tasks from YAML file
    tasks_data = parse_yaml_tasks('tasks.yaml')
    
    project_info = {
        'project': tasks_data.get('project', 'Unknown Project'),
        'owner': tasks_data.get('owner', 'Unknown Owner')
    }
    
    print(f"Creating GitHub issues for: {project_info['project']}")
    print(f"Owner: {project_info['owner']}")
    print("=" * 50)
    
    created_issues = []
    
    # Create issues for each task in each phase
    for phase in tasks_data.get('phases', []):
        phase_name = phase['name']
        print(f"\nProcessing phase: {phase_name}")
        print(f"Goal: {phase.get('goal', 'No goal specified')}")
        
        for task in phase.get('tasks', []):
            issue_url = create_github_issue(task, phase_name, project_info)
            if issue_url:
                created_issues.append({
                    'task_id': task['id'],
                    'title': task['title'],
                    'url': issue_url
                })
    
    # Create milestone issues
    if 'milestones' in tasks_data:
        print(f"\nCreating milestone issues...")
        milestone_urls = create_milestone_issues(tasks_data['milestones'], project_info)
    
    print(f"\n" + "=" * 50)
    print(f"Summary: Created {len(created_issues)} task issues")
    if 'milestones' in tasks_data:
        print(f"Created {len(milestone_urls)} milestone issues")
    
    # Save summary to file
    summary = {
        'project': project_info,
        'created_issues': created_issues,
        'milestone_urls': milestone_urls if 'milestones' in tasks_data else []
    }
    
    with open('github_issues_summary.json', 'w') as f:
        json.dump(summary, f, indent=2)
    
    print(f"\nSummary saved to: github_issues_summary.json")
    print(f"Repository: https://github.com/dylan-mccarthy/Scalable-Process-Agent-System")

if __name__ == "__main__":
    main()