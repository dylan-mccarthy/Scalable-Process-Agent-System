import { fetchRuns } from "@/lib/api";
import type { Run } from "@/types/api";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Activity, CheckCircle2, XCircle, Clock, Ban, PlayCircle } from "lucide-react";

/**
 * Get status icon and color based on run status
 */
function getStatusConfig(status: string) {
  const normalizedStatus = status.toLowerCase();

  switch (normalizedStatus) {
    case "completed":
      return {
        icon: CheckCircle2,
        color: "text-green-600 dark:text-green-400",
        bgColor: "bg-green-100 dark:bg-green-900/30",
        label: "Completed",
      };
    case "failed":
      return {
        icon: XCircle,
        color: "text-red-600 dark:text-red-400",
        bgColor: "bg-red-100 dark:bg-red-900/30",
        label: "Failed",
      };
    case "running":
      return {
        icon: PlayCircle,
        color: "text-blue-600 dark:text-blue-400",
        bgColor: "bg-blue-100 dark:bg-blue-900/30",
        label: "Running",
      };
    case "pending":
    case "scheduled":
      return {
        icon: Clock,
        color: "text-yellow-600 dark:text-yellow-400",
        bgColor: "bg-yellow-100 dark:bg-yellow-900/30",
        label: normalizedStatus === "scheduled" ? "Scheduled" : "Pending",
      };
    case "cancelled":
      return {
        icon: Ban,
        color: "text-gray-600 dark:text-gray-400",
        bgColor: "bg-gray-100 dark:bg-gray-900/30",
        label: "Cancelled",
      };
    default:
      return {
        icon: Activity,
        color: "text-gray-600 dark:text-gray-400",
        bgColor: "bg-gray-100 dark:bg-gray-900/30",
        label: status,
      };
  }
}

/**
 * Calculate duration from run timings or creation time
 */
function calculateDuration(run: Run): string {
  // Check if timings object has duration
  if (run.timings && typeof run.timings.duration === "number") {
    const durationMs = run.timings.duration;

    if (durationMs < 1000) {
      return `${durationMs}ms`;
    } else if (durationMs < 60000) {
      return `${(durationMs / 1000).toFixed(1)}s`;
    } else if (durationMs < 3600000) {
      const minutes = Math.floor(durationMs / 60000);
      const seconds = Math.floor((durationMs % 60000) / 1000);
      return `${minutes}m ${seconds}s`;
    } else {
      const hours = Math.floor(durationMs / 3600000);
      const minutes = Math.floor((durationMs % 3600000) / 60000);
      return `${hours}h ${minutes}m`;
    }
  }

  // For active runs, calculate elapsed time since creation
  const status = run.status.toLowerCase();
  if (status === "running" || status === "pending" || status === "scheduled") {
    const createdAt = new Date(run.createdAt);
    const now = new Date();
    const elapsedMs = now.getTime() - createdAt.getTime();

    if (elapsedMs < 1000) {
      return "< 1s";
    } else if (elapsedMs < 60000) {
      return `${Math.floor(elapsedMs / 1000)}s`;
    } else if (elapsedMs < 3600000) {
      const minutes = Math.floor(elapsedMs / 60000);
      const seconds = Math.floor((elapsedMs % 60000) / 1000);
      return `${minutes}m ${seconds}s`;
    } else {
      const hours = Math.floor(elapsedMs / 3600000);
      const minutes = Math.floor((elapsedMs % 3600000) / 60000);
      return `${hours}h ${minutes}m`;
    }
  }

  return "N/A";
}

/**
 * Format cost information from run
 */
function formatCost(run: Run): string | null {
  if (!run.costs) return null;

  if (typeof run.costs.usd === "number") {
    return `$${run.costs.usd.toFixed(4)}`;
  }

  return null;
}

/**
 * Format token usage from run
 */
function formatTokens(run: Run): string | null {
  if (!run.costs) return null;

  if (typeof run.costs.tokens === "number") {
    return `${run.costs.tokens.toLocaleString()} tokens`;
  }

  return null;
}

/**
 * Runs List Page - Displays latest runs with status and duration
 *
 * This page provides operators with visibility into:
 * - All runs with their current status
 * - Run duration and timing information
 * - Cost and token usage metrics
 * - Error information for failed runs
 */
export default async function RunsListPage() {
  // Fetch runs from Control Plane API using Server Components
  let runs: Run[] = [];
  let error: string | null = null;

  try {
    runs = await fetchRuns();
    // Sort by creation date, newest first
    runs.sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());
  } catch (err) {
    error = err instanceof Error ? err.message : "Failed to fetch runs";
    console.error("Runs list error:", error);
  }

  // Calculate status counts for summary metrics
  const statusCounts = runs.reduce(
    (acc, run) => {
      const status = run.status.toLowerCase();
      acc[status] = (acc[status] || 0) + 1;
      return acc;
    },
    {} as Record<string, number>
  );

  return (
    <div className="bg-background min-h-screen">
      <main className="container mx-auto px-4 py-8">
        <div className="space-y-6">
          {/* Page Header */}
          <div className="space-y-2">
            <h1 className="text-4xl font-bold tracking-tight">Runs</h1>
            <p className="text-muted-foreground text-lg">
              View all runs with status, duration, and metrics
            </p>
          </div>

          {/* Error State */}
          {error && (
            <Card className="border-destructive">
              <CardHeader>
                <CardTitle className="text-destructive">Error Loading Runs</CardTitle>
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

          {/* Summary Metrics */}
          {!error && (
            <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-5">
              <Card>
                <CardHeader className="flex flex-row items-center justify-between pb-2">
                  <CardTitle className="text-sm font-medium">Total Runs</CardTitle>
                  <Activity className="text-muted-foreground h-4 w-4" />
                </CardHeader>
                <CardContent>
                  <div className="text-2xl font-bold">{runs.length}</div>
                </CardContent>
              </Card>

              <Card>
                <CardHeader className="flex flex-row items-center justify-between pb-2">
                  <CardTitle className="text-sm font-medium">Completed</CardTitle>
                  <CheckCircle2 className="h-4 w-4 text-green-600 dark:text-green-400" />
                </CardHeader>
                <CardContent>
                  <div className="text-2xl font-bold">{statusCounts.completed || 0}</div>
                </CardContent>
              </Card>

              <Card>
                <CardHeader className="flex flex-row items-center justify-between pb-2">
                  <CardTitle className="text-sm font-medium">Running</CardTitle>
                  <PlayCircle className="h-4 w-4 text-blue-600 dark:text-blue-400" />
                </CardHeader>
                <CardContent>
                  <div className="text-2xl font-bold">{statusCounts.running || 0}</div>
                </CardContent>
              </Card>

              <Card>
                <CardHeader className="flex flex-row items-center justify-between pb-2">
                  <CardTitle className="text-sm font-medium">Failed</CardTitle>
                  <XCircle className="h-4 w-4 text-red-600 dark:text-red-400" />
                </CardHeader>
                <CardContent>
                  <div className="text-2xl font-bold">{statusCounts.failed || 0}</div>
                </CardContent>
              </Card>

              <Card>
                <CardHeader className="flex flex-row items-center justify-between pb-2">
                  <CardTitle className="text-sm font-medium">Pending</CardTitle>
                  <Clock className="h-4 w-4 text-yellow-600 dark:text-yellow-400" />
                </CardHeader>
                <CardContent>
                  <div className="text-2xl font-bold">
                    {(statusCounts.pending || 0) + (statusCounts.scheduled || 0)}
                  </div>
                </CardContent>
              </Card>
            </div>
          )}

          {/* Runs List */}
          {!error && (
            <Card>
              <CardHeader>
                <CardTitle>All Runs</CardTitle>
                <CardDescription>
                  {runs.length === 0
                    ? "No runs found"
                    : `Showing ${runs.length} run${runs.length === 1 ? "" : "s"}`}
                </CardDescription>
              </CardHeader>
              <CardContent>
                {runs.length === 0 ? (
                  <p className="text-muted-foreground py-8 text-center">
                    No runs have been created yet
                  </p>
                ) : (
                  <div className="space-y-3">
                    {runs.map((run) => {
                      const statusConfig = getStatusConfig(run.status);
                      const Icon = statusConfig.icon;
                      const duration = calculateDuration(run);
                      const cost = formatCost(run);
                      const tokens = formatTokens(run);

                      return (
                        <div
                          key={run.runId}
                          className="border-border hover:bg-muted/50 rounded-lg border p-4 transition-colors"
                        >
                          <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
                            {/* Left side: Run ID and Status */}
                            <div className="flex-1 space-y-2">
                              <div className="flex items-center gap-3">
                                <h3 className="font-mono text-sm font-semibold">{run.runId}</h3>
                                <span
                                  className={`flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-medium ${statusConfig.bgColor}`}
                                >
                                  <Icon className={`h-3 w-3 ${statusConfig.color}`} />
                                  <span className={statusConfig.color}>{statusConfig.label}</span>
                                </span>
                              </div>

                              {/* Run Details */}
                              <div className="text-muted-foreground flex flex-wrap gap-x-4 gap-y-1 text-sm">
                                <span className="flex items-center gap-1">
                                  <span className="text-foreground font-medium">Agent:</span>
                                  {run.agentId}
                                </span>
                                <span className="flex items-center gap-1">
                                  <span className="text-foreground font-medium">Version:</span>
                                  {run.version}
                                </span>
                                {run.nodeId && (
                                  <span className="flex items-center gap-1">
                                    <span className="text-foreground font-medium">Node:</span>
                                    {run.nodeId}
                                  </span>
                                )}
                                <span className="flex items-center gap-1">
                                  <span className="text-foreground font-medium">Created:</span>
                                  {new Date(run.createdAt).toLocaleString()}
                                </span>
                              </div>

                              {/* Error Info for Failed Runs */}
                              {run.status.toLowerCase() === "failed" && run.errorInfo && (
                                <div className="text-destructive bg-destructive/10 mt-2 rounded-md p-2 text-sm">
                                  <span className="font-medium">Error:</span>{" "}
                                  {String(
                                    run.errorInfo.errorMessage ||
                                      run.errorInfo.message ||
                                      "Unknown error"
                                  )}
                                </div>
                              )}
                            </div>

                            {/* Right side: Metrics */}
                            <div className="flex gap-6 text-sm lg:flex-shrink-0">
                              <div className="text-center">
                                <div className="text-muted-foreground text-xs">Duration</div>
                                <div className="font-semibold">{duration}</div>
                              </div>
                              {tokens && (
                                <div className="text-center">
                                  <div className="text-muted-foreground text-xs">Tokens</div>
                                  <div className="font-semibold">{tokens}</div>
                                </div>
                              )}
                              {cost && (
                                <div className="text-center">
                                  <div className="text-muted-foreground text-xs">Cost</div>
                                  <div className="font-semibold">{cost}</div>
                                </div>
                              )}
                            </div>
                          </div>
                        </div>
                      );
                    })}
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
