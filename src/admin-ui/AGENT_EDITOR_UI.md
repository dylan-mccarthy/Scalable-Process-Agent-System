# Agent Editor UI - Implementation Summary

## Overview

The Agent Editor provides a comprehensive form-based interface for creating and editing business process agent definitions with Azure AI Foundry model integration.

## Pages Implemented

### 1. Agents List Page (`/agents`)

**Route:** `/agents`

**Features:**
- Displays all agent definitions in a list
- Each agent card shows:
  - Agent name and ID
  - Description (if provided)
  - Selected model (e.g., "Model: gpt-4")
  - Number of configured tools
- "Create Agent" button in the top-right corner
- "Edit" button for each agent
- Empty state with helpful message when no agents exist
- Error state with API connection information

**Layout:**
- Full-width page with container max-width
- Grid layout for agent cards
- Responsive design (mobile-friendly)

### 2. Create New Agent Page (`/agents/new`)

**Route:** `/agents/new`

**Features:**
- Displays the AgentEditorForm component in "create" mode
- Page header: "Create New Agent"
- Subtitle: "Define a new business process agent with Azure AI Foundry models"
- Form with all configuration sections (detailed below)

### 3. Edit Agent Page (`/agents/[agentId]/edit`)

**Route:** `/agents/{agentId}/edit` (e.g., `/agents/invoice-classifier/edit`)

**Features:**
- Pre-populates form with existing agent data
- Page header: "Edit Agent"
- Subtitle shows agent name being edited
- Same form interface as create page
- Updates existing agent on save

## Agent Editor Form Component

The `AgentEditorForm` is a comprehensive React component with the following sections:

### Section 1: Basic Information

**Fields:**
- **Name** (required)
  - Text input
  - Placeholder: "Invoice Classifier"
  - Max length: 100 characters
  - Validation: Required, min 1 character

- **Description** (optional)
  - Textarea (3-4 rows)
  - Placeholder: "Classifies incoming invoices and routes them appropriately"
  - Max length: 500 characters

- **Instructions (System Prompt)** (required)
  - Large textarea (8+ rows)
  - Monospace font for better readability
  - Placeholder: "You are an AI assistant that classifies invoices..."
  - Max length: 10,000 characters
  - Validation: Required, min 1 character
  - Description: "System prompt that defines the agent's behavior and role"

### Section 2: Azure AI Foundry Model Configuration

**Fields:**
- **Model** (required)
  - Dropdown select
  - Options:
    - GPT-4
    - GPT-4 Turbo
    - GPT-4o
    - GPT-3.5 Turbo
  - Validation: Required
  - Description: "Azure OpenAI model to use for this agent"

- **Temperature** (optional)
  - Number input
  - Range: 0.0 to 2.0
  - Step: 0.1
  - Default: 0.7
  - Description: "Controls randomness. Lower is more focused, higher is more creative (0-2, default 0.7)"

### Section 3: Budget Constraints

**Purpose:** Set limits on resource usage per agent run

**Fields:**
- **Max Tokens** (optional)
  - Number input
  - Range: 1 to 100,000
  - Default: 4,000
  - Description: "Maximum tokens the agent can use per run (default 4000)"

- **Max Duration (seconds)** (optional)
  - Number input
  - Range: 1 to 3,600 (1 hour)
  - Default: 60
  - Description: "Maximum time the agent can run in seconds (default 60)"

### Section 4: Azure AI Foundry Tools

**Purpose:** Select built-in tools to enhance agent capabilities

**Interface:**
- Visual multi-select with clickable cards
- Grid layout (2 columns on desktop, 1 on mobile)
- Each tool card displays:
  - Tool name
  - Brief description
  - Checkbox indicator (visual only, styled)
  - Highlight on selection (border and background color)

**Available Tools (8 total):**

1. **Code Interpreter**
   - Description: "Run Python code in sandbox"
   - Capabilities: Python execution, data analysis, file generation

2. **File Search**
   - Description: "RAG with semantic search"
   - Capabilities: Document search, RAG implementation

3. **Azure AI Search**
   - Description: "Enterprise search system"
   - Capabilities: Vector search, full-text search, hybrid search

4. **Bing Grounding**
   - Description: "Real-time web search"
   - Capabilities: Web search, real-time information

5. **Function Calling**
   - Description: "Custom function integration"
   - Capabilities: Custom business logic integration

6. **Azure Functions**
   - Description: "Serverless code execution"
   - Capabilities: Event-driven, async operations

7. **OpenAPI**
   - Description: "Connect to REST APIs"
   - Capabilities: API integration via OpenAPI spec

8. **Browser Automation**
   - Description: "Automated browsing"
   - Capabilities: Web navigation, interaction, data extraction

**Selection Feedback:**
- Shows count of selected tools below the grid
- Example: "3 tools selected"

### Form Actions

**Buttons:**
- **Cancel** (outline style)
  - Navigates back to `/agents` without saving
  - Enabled at all times

- **Create Agent / Update Agent** (primary style)
  - Submit button
  - Text changes based on mode (create vs. edit)
  - Shows "Saving..." during submission
  - Disabled during submission

## Form Validation

**Client-side validation using Zod:**
- All required fields must be filled
- Field length constraints enforced
- Number ranges validated
- Immediate feedback on validation errors
- Error messages displayed below fields

**Server-side validation:**
- API returns errors for invalid data
- Errors displayed in a card at top of form
- Specific error messages from API shown to user

## User Experience Features

1. **Responsive Design**
   - Mobile-friendly layout
   - Stacks vertically on small screens
   - Touch-friendly controls

2. **Accessibility**
   - Proper form labels
   - ARIA descriptions
   - Keyboard navigation support
   - Error message associations

3. **Visual Feedback**
   - Loading states during submission
   - Success redirect to list page
   - Error messages with styling
   - Tool selection highlights

4. **Form State Management**
   - React Hook Form for state
   - Controlled inputs
   - Default values from existing agent (edit mode)

## Navigation

The main header includes an "Agents" navigation link:
- Header navigation: Home | Fleet | Runs | **Agents**
- Clicking "Agents" navigates to `/agents`
- Always visible in the application header

## Technical Implementation

**Technologies:**
- Next.js 14+ (App Router)
- React 19 with hooks
- TypeScript (strict mode)
- Tailwind CSS for styling
- shadcn/ui components
- React Hook Form for form management
- Zod for validation
- Client component for interactivity

**File Structure:**
```
src/admin-ui/
├── src/
│   ├── app/
│   │   └── agents/
│   │       ├── page.tsx              # Agents list
│   │       ├── new/
│   │       │   └── page.tsx          # Create agent
│   │       └── [agentId]/
│   │           └── edit/
│   │               └── page.tsx      # Edit agent
│   ├── components/
│   │   ├── agents/
│   │   │   └── agent-editor-form.tsx # Main form component
│   │   └── ui/
│   │       ├── form.tsx              # Form primitives
│   │       ├── input.tsx             # Input component
│   │       ├── textarea.tsx          # Textarea component
│   │       ├── select.tsx            # Select component
│   │       └── label.tsx             # Label component
│   ├── lib/
│   │   └── api.ts                    # Agent API functions
│   └── types/
│       └── api.ts                    # Agent type definitions
```

## API Integration

**Endpoints used:**
- `GET /v1/agents` - Fetch all agents
- `GET /v1/agents/{agentId}` - Fetch specific agent
- `POST /v1/agents` - Create new agent
- `PUT /v1/agents/{agentId}` - Update existing agent

**Request/Response:**
- JSON format
- Proper error handling
- Loading states
- Success redirects

## Form Data Mapping

The form transforms UI data to API format:

```typescript
{
  name: string,
  description?: string,
  instructions: string,
  modelProfile: {
    model: string,      // e.g., "gpt-4"
    temperature: number // e.g., 0.7
  },
  budget: {
    maxTokens?: number,
    maxDurationSeconds?: number
  },
  tools?: string[]     // e.g., ["CodeInterpreter", "FileSearch"]
}
```

## Future Enhancements

Potential improvements not in current scope:
- Real-time validation with API
- Draft saving
- Agent cloning
- Version history viewing
- Connector configuration UI
- Advanced tool configuration options
- Bulk operations
- Import/export agent definitions
