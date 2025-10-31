import { AgentEditorForm } from "@/components/agents/agent-editor-form";

/**
 * Create New Agent Page
 *
 * Provides a form for creating new agent definitions with:
 * - Basic information (name, description, instructions)
 * - Azure AI Foundry model selection
 * - Budget constraints
 * - Tool selection
 */
export default function NewAgentPage() {
  return (
    <div className="bg-background min-h-screen">
      <main className="container mx-auto px-4 py-8">
        <div className="mx-auto max-w-4xl space-y-6">
          {/* Page Header */}
          <div className="space-y-2">
            <h1 className="text-4xl font-bold tracking-tight">Create New Agent</h1>
            <p className="text-muted-foreground text-lg">
              Define a new business process agent with Azure AI Foundry models
            </p>
          </div>

          {/* Agent Editor Form */}
          <AgentEditorForm mode="create" />
        </div>
      </main>
    </div>
  );
}
