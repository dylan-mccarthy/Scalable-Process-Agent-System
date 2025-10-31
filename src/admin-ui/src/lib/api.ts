/**
 * API client functions for Business Process Agents Control Plane
 */

import type { Node, Run, Agent, CreateAgentRequest, UpdateAgentRequest } from "@/types/api";

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000";

/**
 * Run statuses that are considered "active"
 */
export const ACTIVE_RUN_STATUSES = ["pending", "running", "scheduled"];

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
  return runs.filter((run) => ACTIVE_RUN_STATUSES.includes(run.status.toLowerCase()));
}

/**
 * Fetch all agents from the Control Plane API
 */
export async function fetchAgents(): Promise<Agent[]> {
  const response = await fetch(`${API_BASE_URL}/v1/agents`, {
    headers: {
      "Content-Type": "application/json",
    },
  });

  if (!response.ok) {
    throw new Error(`Failed to fetch agents: ${response.statusText}`);
  }

  return response.json();
}

/**
 * Fetch a specific agent from the Control Plane API
 */
export async function fetchAgent(agentId: string): Promise<Agent> {
  const response = await fetch(`${API_BASE_URL}/v1/agents/${agentId}`, {
    headers: {
      "Content-Type": "application/json",
    },
  });

  if (!response.ok) {
    throw new Error(`Failed to fetch agent: ${response.statusText}`);
  }

  return response.json();
}

/**
 * Create a new agent
 */
export async function createAgent(request: CreateAgentRequest): Promise<Agent> {
  const response = await fetch(`${API_BASE_URL}/v1/agents`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify(request),
  });

  if (!response.ok) {
    const errorData = await response.json().catch(() => ({}));
    throw new Error(errorData.error || `Failed to create agent: ${response.statusText}`);
  }

  return response.json();
}

/**
 * Update an existing agent
 */
export async function updateAgent(
  agentId: string,
  request: UpdateAgentRequest
): Promise<Agent> {
  const response = await fetch(`${API_BASE_URL}/v1/agents/${agentId}`, {
    method: "PUT",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify(request),
  });

  if (!response.ok) {
    const errorData = await response.json().catch(() => ({}));
    throw new Error(errorData.error || `Failed to update agent: ${response.statusText}`);
  }

  return response.json();
}

/**
 * Delete an agent
 */
export async function deleteAgent(agentId: string): Promise<void> {
  const response = await fetch(`${API_BASE_URL}/v1/agents/${agentId}`, {
    method: "DELETE",
    headers: {
      "Content-Type": "application/json",
    },
  });

  if (!response.ok) {
    throw new Error(`Failed to delete agent: ${response.statusText}`);
  }
}
