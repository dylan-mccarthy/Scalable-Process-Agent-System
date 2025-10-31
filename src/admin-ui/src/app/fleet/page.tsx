import { fetchNodes, fetchRuns, getActiveRuns } from "@/lib/api";
import type { Node, Run } from "@/types/api";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Activity, Cpu, Server } from "lucide-react";

/**
 * Fleet Dashboard Page - Displays nodes and active runs
 * 
 * This page provides operators with real-time visibility into:
 * - Total number of registered nodes
 * - Total active runs across the fleet
 * - Individual node status and capacity
 * - List of currently active runs with details
 */
export default async function FleetDashboardPage() {
  // Fetch data from Control Plane API using Server Components
  let nodes: Node[] = [];
  let activeRuns: Run[] = [];
  let error: string | null = null;

  try {
    const [nodesData, runsData] = await Promise.all([
      fetchNodes(),
      fetchRuns(),
    ]);

    nodes = nodesData;
    activeRuns = getActiveRuns(runsData);
  } catch (err) {
    error = err instanceof Error ? err.message : "Failed to fetch data";
    console.error("Fleet dashboard error:", error);
  }

  // Calculate metrics only when we have valid data
  const totalActiveRuns = !error
    ? nodes.reduce((sum, node) => sum + node.status.activeRuns, 0)
    : 0;
  const totalAvailableSlots = !error
    ? nodes.reduce((sum, node) => sum + node.status.availableSlots, 0)
    : 0;

  return (
    <div className="bg-background min-h-screen">
      <main className="container mx-auto px-4 py-8">
        <div className="space-y-6">
          {/* Page Header */}
          <div className="space-y-2">
            <h1 className="text-4xl font-bold tracking-tight">Fleet Dashboard</h1>
            <p className="text-muted-foreground text-lg">
              Monitor nodes and active runs across the platform
            </p>
          </div>

          {/* Error State */}
          {error && (
            <Card className="border-destructive">
              <CardHeader>
                <CardTitle className="text-destructive">Error Loading Fleet Data</CardTitle>
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

          {/* Fleet Metrics */}
          {!error && (
            <div className="grid gap-4 md:grid-cols-3">
              <Card>
                <CardHeader className="flex flex-row items-center justify-between pb-2">
                  <CardTitle className="text-sm font-medium">Total Nodes</CardTitle>
                  <Server className="text-muted-foreground h-4 w-4" />
                </CardHeader>
                <CardContent>
                  <div className="text-2xl font-bold">{nodes.length}</div>
                  <p className="text-muted-foreground text-xs">
                    {nodes.filter((n) => n.status.state === "active").length} active
                  </p>
                </CardContent>
              </Card>

              <Card>
                <CardHeader className="flex flex-row items-center justify-between pb-2">
                  <CardTitle className="text-sm font-medium">Active Runs</CardTitle>
                  <Activity className="text-muted-foreground h-4 w-4" />
                </CardHeader>
                <CardContent>
                  <div className="text-2xl font-bold">{totalActiveRuns}</div>
                  <p className="text-muted-foreground text-xs">
                    From node status reports
                  </p>
                </CardContent>
              </Card>

              <Card>
                <CardHeader className="flex flex-row items-center justify-between pb-2">
                  <CardTitle className="text-sm font-medium">Available Slots</CardTitle>
                  <Cpu className="text-muted-foreground h-4 w-4" />
                </CardHeader>
                <CardContent>
                  <div className="text-2xl font-bold">{totalAvailableSlots}</div>
                  <p className="text-muted-foreground text-xs">
                    Capacity for new runs
                  </p>
                </CardContent>
              </Card>
            </div>
          )}

          {/* Nodes List */}
          {!error && (
            <Card>
              <CardHeader>
                <CardTitle>Registered Nodes</CardTitle>
                <CardDescription>
                  Worker nodes registered with the control plane
                </CardDescription>
              </CardHeader>
              <CardContent>
                {nodes.length === 0 ? (
                  <p className="text-muted-foreground text-center py-8">
                    No nodes registered yet
                  </p>
                ) : (
                  <div className="space-y-4">
                    {nodes.map((node) => (
                      <div
                        key={node.nodeId}
                        className="border-border flex items-center justify-between rounded-lg border p-4"
                      >
                        <div className="space-y-1">
                          <div className="flex items-center gap-2">
                            <h3 className="font-semibold">{node.nodeId}</h3>
                            <span
                              className={`rounded-full px-2 py-0.5 text-xs font-medium ${
                                node.status.state === "active"
                                  ? "bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-100"
                                  : "bg-gray-100 text-gray-800 dark:bg-gray-900 dark:text-gray-100"
                              }`}
                            >
                              {node.status.state}
                            </span>
                          </div>
                          <p className="text-muted-foreground text-sm">
                            Last heartbeat:{" "}
                            {new Date(node.heartbeatAt).toLocaleString()}
                          </p>
                        </div>
                        <div className="flex gap-6 text-sm">
                          <div className="text-center">
                            <div className="text-muted-foreground text-xs">Active</div>
                            <div className="font-semibold">{node.status.activeRuns}</div>
                          </div>
                          <div className="text-center">
                            <div className="text-muted-foreground text-xs">Available</div>
                            <div className="font-semibold">{node.status.availableSlots}</div>
                          </div>
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </CardContent>
            </Card>
          )}

          {/* Active Runs */}
          {!error && (
            <Card>
              <CardHeader>
                <CardTitle>Active Runs</CardTitle>
                <CardDescription>
                  Currently executing or scheduled runs
                </CardDescription>
              </CardHeader>
              <CardContent>
                {activeRuns.length === 0 ? (
                  <p className="text-muted-foreground text-center py-8">
                    No active runs at the moment
                  </p>
                ) : (
                  <div className="space-y-4">
                    {activeRuns.map((run) => (
                      <div
                        key={run.runId}
                        className="border-border flex items-center justify-between rounded-lg border p-4"
                      >
                        <div className="space-y-1">
                          <div className="flex items-center gap-2">
                            <h3 className="font-mono text-sm font-semibold">
                              {run.runId}
                            </h3>
                            <span
                              className={`rounded-full px-2 py-0.5 text-xs font-medium ${
                                run.status === "running"
                                  ? "bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-100"
                                  : run.status === "pending"
                                    ? "bg-yellow-100 text-yellow-800 dark:bg-yellow-900 dark:text-yellow-100"
                                    : "bg-gray-100 text-gray-800 dark:bg-gray-900 dark:text-gray-100"
                              }`}
                            >
                              {run.status}
                            </span>
                          </div>
                          <div className="text-muted-foreground flex gap-4 text-sm">
                            <span>Agent: {run.agentId}</span>
                            {run.nodeId && <span>Node: {run.nodeId}</span>}
                            <span>
                              Started: {new Date(run.createdAt).toLocaleString()}
                            </span>
                          </div>
                        </div>
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
