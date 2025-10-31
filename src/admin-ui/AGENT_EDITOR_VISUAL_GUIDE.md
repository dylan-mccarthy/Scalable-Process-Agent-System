# Agent Editor UI - Visual Guide

This document provides a visual description of the Agent Editor UI implementation.

## Page Layouts

### 1. Agents List Page (`/agents`)

```
┌─────────────────────────────────────────────────────────────────┐
│ [Header with Navigation: Fleet | Runs | Agents]                 │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Agent Definitions                         [+ Create Agent]     │
│  Manage your business process agents                            │
│                                                                  │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │ All Agents                                               │   │
│  │ 3 agents configured                                      │   │
│  ├─────────────────────────────────────────────────────────┤   │
│  │ ┌─────────────────────────────────────────────────────┐ │   │
│  │ │ Invoice Classifier      [agent-001]         [Edit]  │ │   │
│  │ │ Classifies incoming invoices...                     │ │   │
│  │ │ Model: gpt-4    2 tools                             │ │   │
│  │ └─────────────────────────────────────────────────────┘ │   │
│  │ ┌─────────────────────────────────────────────────────┐ │   │
│  │ │ Document Analyzer       [agent-002]         [Edit]  │ │   │
│  │ │ Analyzes various document types...                  │ │   │
│  │ │ Model: gpt-4-turbo    4 tools                       │ │   │
│  │ └─────────────────────────────────────────────────────┘ │   │
│  │ ┌─────────────────────────────────────────────────────┐ │   │
│  │ │ Email Responder         [agent-003]         [Edit]  │ │   │
│  │ │ Generates automated email responses...              │ │   │
│  │ │ Model: gpt-35-turbo    1 tool                       │ │   │
│  │ └─────────────────────────────────────────────────────┘ │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 2. Create New Agent Page (`/agents/new`)

```
┌─────────────────────────────────────────────────────────────────┐
│ [Header with Navigation]                                        │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Create New Agent                                               │
│  Define a new business process agent with Azure AI Foundry      │
│                                                                  │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │ Basic Information                                        │   │
│  │ Configure the agent's identity and purpose              │   │
│  ├─────────────────────────────────────────────────────────┤   │
│  │ Name *                                                   │   │
│  │ [Invoice Classifier                                   ]  │   │
│  │ A descriptive name for your agent                       │   │
│  │                                                          │   │
│  │ Description                                              │   │
│  │ [Classifies incoming invoices and routes them        ]  │   │
│  │ [appropriately                                        ]  │   │
│  │ Optional description of what this agent does            │   │
│  │                                                          │   │
│  │ Instructions (System Prompt) *                          │   │
│  │ [You are an AI assistant that classifies invoices   ]  │   │
│  │ [based on their content and metadata. Analyze the    ]  │   │
│  │ [invoice details and categorize them into:           ]  │   │
│  │ [- Purchase Orders                                   ]  │   │
│  │ [- Expense Reports                                   ]  │   │
│  │ [- Service Invoices                                  ]  │   │
│  │ [...                                                 ]  │   │
│  │ System prompt that defines the agent's behavior         │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                  │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │ Azure AI Foundry Model Configuration                    │   │
│  │ Select the LLM model and configure its parameters       │   │
│  ├─────────────────────────────────────────────────────────┤   │
│  │ Model *                                                  │   │
│  │ [GPT-4                                            ▼]     │   │
│  │ Azure OpenAI model to use for this agent                │   │
│  │                                                          │   │
│  │ Temperature                                              │   │
│  │ [0.7                                              ]      │   │
│  │ Controls randomness. Lower is more focused (0-2)        │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                  │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │ Budget Constraints                                       │   │
│  │ Set limits on resource usage per agent run              │   │
│  ├─────────────────────────────────────────────────────────┤   │
│  │ Max Tokens                                               │   │
│  │ [4000                                             ]      │   │
│  │ Maximum tokens the agent can use per run                │   │
│  │                                                          │   │
│  │ Max Duration (seconds)                                   │   │
│  │ [60                                               ]      │   │
│  │ Maximum time the agent can run in seconds               │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                  │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │ Azure AI Foundry Tools                                   │   │
│  │ Select built-in tools to enhance your agent's           │   │
│  │ capabilities                                             │   │
│  ├─────────────────────────────────────────────────────────┤   │
│  │ ┌─────────────────────┐  ┌─────────────────────┐       │   │
│  │ │ Code Interpreter [✓]│  │ File Search      [✓]│       │   │
│  │ │ Run Python code in  │  │ RAG with semantic   │       │   │
│  │ │ sandbox             │  │ search              │       │   │
│  │ └─────────────────────┘  └─────────────────────┘       │   │
│  │ ┌─────────────────────┐  ┌─────────────────────┐       │   │
│  │ │ Azure AI Search  [ ]│  │ Bing Grounding   [ ]│       │   │
│  │ │ Enterprise search   │  │ Real-time web       │       │   │
│  │ │ system              │  │ search              │       │   │
│  │ └─────────────────────┘  └─────────────────────┘       │   │
│  │ ┌─────────────────────┐  ┌─────────────────────┐       │   │
│  │ │ Function Calling [ ]│  │ Azure Functions  [ ]│       │   │
│  │ │ Custom function     │  │ Serverless code     │       │   │
│  │ │ integration         │  │ execution           │       │   │
│  │ └─────────────────────┘  └─────────────────────┘       │   │
│  │ ┌─────────────────────┐  ┌─────────────────────┐       │   │
│  │ │ OpenAPI          [ ]│  │ Browser Automation[]│       │   │
│  │ │ Connect to REST     │  │ Automated browsing  │       │   │
│  │ │ APIs                │  │                     │       │   │
│  │ └─────────────────────┘  └─────────────────────┘       │   │
│  │                                                          │   │
│  │ 2 tools selected                                        │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                  │
│                                    [Cancel]  [Create Agent]     │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

## Visual Design Elements

### Color Scheme
- **Background**: Clean white/light gray
- **Primary**: Blue accent for buttons and selections
- **Borders**: Light gray for cards and inputs
- **Success**: Green for selected tools
- **Error**: Red for validation errors
- **Text**: 
  - Headings: Dark gray/black
  - Body: Medium gray
  - Muted: Light gray

### Typography
- **Headings**: Bold, large (32-40px for h1)
- **Subheadings**: Medium weight, 16-18px
- **Body Text**: Regular weight, 14-16px
- **Code/Monospace**: For agent IDs and instructions field

### Interactive Elements

#### Tool Selection Cards
```
Not Selected:                 Selected:
┌─────────────────────┐      ┌─────────────────────┐
│ Code Interpreter [ ]│      │ Code Interpreter [✓]│ (Blue border)
│ Run Python code in  │      │ Run Python code in  │ (Blue background)
│ sandbox             │      │ sandbox             │
└─────────────────────┘      └─────────────────────┘
```

#### Form Inputs
```
Normal State:
┌─────────────────────────────────────────────┐
│ Invoice Classifier                          │
└─────────────────────────────────────────────┘

Error State:
┌─────────────────────────────────────────────┐ (Red border)
│                                             │
└─────────────────────────────────────────────┘
❌ Name is required (Red text)

Focused State:
┌─────────────────────────────────────────────┐ (Blue border)
│ Invoice Classifier█                         │
└─────────────────────────────────────────────┘
```

#### Buttons
```
Primary:                Cancel:
┌──────────────────┐   ┌──────────────────┐
│ Create Agent     │   │ Cancel           │ (Outlined)
└──────────────────┘   └──────────────────┘
(Blue background)      (White background)

Disabled:
┌──────────────────┐
│ Saving...        │ (Grayed out)
└──────────────────┘
```

### Responsive Behavior

**Desktop (1024px+)**
- Two-column tool grid
- Wide form fields
- Side-by-side buttons

**Tablet (768-1023px)**
- Two-column tool grid
- Full-width form fields
- Stacked buttons

**Mobile (< 768px)**
- Single-column tool grid
- Full-width everything
- Stacked buttons
- Reduced padding

## Accessibility Features

1. **Keyboard Navigation**
   - Tab through all form fields
   - Enter to submit
   - Escape to cancel (if in modal)
   - Arrow keys in selects

2. **Screen Readers**
   - Proper ARIA labels on all inputs
   - Form validation errors announced
   - Tool selection state announced
   - Loading states communicated

3. **Visual Indicators**
   - Clear focus states (blue outline)
   - Error messages with icons
   - Required field indicators (*)
   - Success/loading feedback

## User Interaction Flow

### Creating a New Agent
1. Click "Create Agent" button on list page
2. Fill in Basic Information (name, description, instructions)
3. Select Azure AI Foundry model and temperature
4. Configure budget constraints (optional)
5. Select desired tools by clicking cards
6. Review selections
7. Click "Create Agent"
8. Loading state shows "Saving..."
9. Success: Redirect to agents list
10. Error: Show error message at top of form

### Editing an Agent
1. Click "Edit" button on agent card
2. Form pre-populated with current values
3. Modify any fields
4. Tool cards show current selections
5. Click "Update Agent"
6. Same save flow as create

### Validation Feedback
- Real-time validation on blur
- Error messages appear below fields
- Form submission blocked if errors exist
- Clear visual indicators for required fields
- Number inputs show range constraints

## Empty States

### No Agents
```
┌─────────────────────────────────────────────┐
│                                             │
│            📄                               │
│                                             │
│   No agent definitions found.               │
│   Create your first agent to get started.  │
│                                             │
│        [+ Create First Agent]               │
│                                             │
└─────────────────────────────────────────────┘
```

### API Error
```
┌─────────────────────────────────────────────┐
│ ⚠️ Error Loading Agents                     │ (Red border)
│                                             │
│ Failed to fetch agents: Network error       │
│                                             │
│ Make sure the Control Plane API is running │
│ at http://localhost:5000                    │
└─────────────────────────────────────────────┘
```

This visual guide complements the technical documentation and provides a clear understanding of the UI implementation.
