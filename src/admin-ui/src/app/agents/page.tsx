import Link from "next/link";
import { fetchAgents } from "@/lib/api";
import type { Agent } from "@/types/api";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Plus, FileText } from "lucide-react";

/**
 * Agents List Page - Displays all agents and allows navigation to editor
 *
 * This page provides operators with:
 * - List of all agent definitions
 * - Quick view of agent details
 * - Navigation to create new agents or edit existing ones
 */
export default async function AgentsPage() {
  let agents: Agent[] = [];
  let error: string | null = null;

  try {
    agents = await fetchAgents();
  } catch (err) {
    error = err instanceof Error ? err.message : "Failed to fetch agents";
    console.error("Agents list error:", error);
  }

  return (
    <div className="bg-background min-h-screen">
      <main className="container mx-auto px-4 py-8">
        <div className="space-y-6">
          {/* Page Header */}
          <div className="flex items-center justify-between">
            <div className="space-y-2">
              <h1 className="text-4xl font-bold tracking-tight">Agent Definitions</h1>
              <p className="text-muted-foreground text-lg">
                Manage your business process agents and their configurations
              </p>
            </div>
            <Link href="/agents/new">
              <Button size="lg">
                <Plus className="mr-2 h-4 w-4" />
                Create Agent
              </Button>
            </Link>
          </div>

          {/* Error State */}
          {error && (
            <Card className="border-destructive">
              <CardHeader>
                <CardTitle className="text-destructive">Error Loading Agents</CardTitle>
                <CardDescription>{error}</CardDescription>
              </CardHeader>
              <CardContent>
                <p className="text-muted-foreground text-sm">
                  Make sure the Control Plane API is running at{" "}
                  <code className="bg-muted rounded px-1 py-0.5">
                    {process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000"}
                  </code>
                </p>
              </CardContent>
            </Card>
          )}

          {/* Agents List */}
          {!error && (
            <Card>
              <CardHeader>
                <CardTitle>All Agents</CardTitle>
                <CardDescription>
                  {agents.length === 0
                    ? "No agents defined yet"
                    : `${agents.length} agent${agents.length === 1 ? "" : "s"} configured`}
                </CardDescription>
              </CardHeader>
              <CardContent>
                {agents.length === 0 ? (
                  <div className="flex flex-col items-center justify-center py-12">
                    <FileText className="text-muted-foreground mb-4 h-12 w-12" />
                    <p className="text-muted-foreground mb-4 text-center">
                      No agent definitions found.
                      <br />
                      Create your first agent to get started.
                    </p>
                    <Link href="/agents/new">
                      <Button>
                        <Plus className="mr-2 h-4 w-4" />
                        Create First Agent
                      </Button>
                    </Link>
                  </div>
                ) : (
                  <div className="space-y-4">
                    {agents.map((agent) => (
                      <div
                        key={agent.agentId}
                        className="border-border flex items-center justify-between rounded-lg border p-4 hover:bg-accent transition-colors"
                      >
                        <div className="flex-1 space-y-1">
                          <div className="flex items-center gap-2">
                            <h3 className="font-semibold">{agent.name}</h3>
                            <span className="text-muted-foreground font-mono text-xs">
                              {agent.agentId}
                            </span>
                          </div>
                          {agent.description && (
                            <p className="text-muted-foreground text-sm">{agent.description}</p>
                          )}
                          <div className="flex gap-4 text-xs text-muted-foreground">
                            {agent.modelProfile?.model != null &&
                              typeof agent.modelProfile.model === "string" && (
                                <span>Model: {agent.modelProfile.model}</span>
                              )}
                            {agent.tools && agent.tools.length > 0 && (
                              <span>
                                {agent.tools.length} tool{agent.tools.length === 1 ? "" : "s"}
                              </span>
                            )}
                          </div>
                        </div>
                        <Link href={`/agents/${agent.agentId}/edit`}>
                          <Button variant="outline">Edit</Button>
                        </Link>
                      </div>
                    ))}
                  </div>
                )}
              </CardContent>
            </Card>
          )}
        </div>
      </main>
    </div>
  );
}
