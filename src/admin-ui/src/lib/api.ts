/**
 * API client functions for Business Process Agents Control Plane
 */

import type { Node, Run } from "@/types/api";

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000";

/**
 * Fetch all nodes from the Control Plane API
 */
export async function fetchNodes(): Promise<Node[]> {
  const response = await fetch(`${API_BASE_URL}/v1/nodes`, {
    headers: {
      "Content-Type": "application/json",
    },
  });

  if (!response.ok) {
    throw new Error(`Failed to fetch nodes: ${response.statusText}`);
  }

  return response.json();
}

/**
 * Fetch all runs from the Control Plane API
 */
export async function fetchRuns(): Promise<Run[]> {
  const response = await fetch(`${API_BASE_URL}/v1/runs`, {
    headers: {
      "Content-Type": "application/json",
    },
  });

  if (!response.ok) {
    throw new Error(`Failed to fetch runs: ${response.statusText}`);
  }

  return response.json();
}

/**
 * Get active runs (status: pending, running, or scheduled)
 */
export function getActiveRuns(runs: Run[]): Run[] {
  return runs.filter((run) =>
    ["pending", "running", "scheduled"].includes(run.status.toLowerCase())
  );
}
