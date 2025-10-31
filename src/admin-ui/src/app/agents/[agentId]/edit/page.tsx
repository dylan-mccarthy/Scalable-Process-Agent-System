import { notFound } from "next/navigation";
import { fetchAgent } from "@/lib/api";
import { AgentEditorForm } from "@/components/agents/agent-editor-form";

/**
 * Edit Agent Page
 *
 * Provides a form for editing existing agent definitions with:
 * - Pre-populated values from the existing agent
 * - Azure AI Foundry model selection
 * - Budget constraints
 * - Tool selection
 */
export default async function EditAgentPage({
  params,
}: {
  params: Promise<{ agentId: string }>;
}) {
  const { agentId } = await params;
  let agent;

  try {
    agent = await fetchAgent(agentId);
  } catch (error) {
    console.error("Error fetching agent:", error);
    notFound();
  }

  return (
    <div className="bg-background min-h-screen">
      <main className="container mx-auto px-4 py-8">
        <div className="mx-auto max-w-4xl space-y-6">
          {/* Page Header */}
          <div className="space-y-2">
            <h1 className="text-4xl font-bold tracking-tight">Edit Agent</h1>
            <p className="text-muted-foreground text-lg">
              Update configuration for <span className="font-semibold">{agent.name}</span>
            </p>
          </div>

          {/* Agent Editor Form */}
          <AgentEditorForm agent={agent} mode="edit" />
        </div>
      </main>
    </div>
  );
}
