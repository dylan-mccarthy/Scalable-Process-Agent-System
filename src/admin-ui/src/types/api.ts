/**
 * API type definitions for Business Process Agents
 * These types mirror the backend models from ControlPlane.Api
 */

export interface NodeStatus {
  state: string;
  activeRuns: number;
  availableSlots: number;
}

export interface Node {
  nodeId: string;
  metadata?: Record<string, unknown>;
  capacity?: Record<string, unknown>;
  status: NodeStatus;
  heartbeatAt: string;
}

export interface Run {
  runId: string;
  agentId: string;
  version: string;
  deploymentId?: string;
  nodeId?: string;
  inputRef?: Record<string, unknown>;
  status: string;
  timings?: Record<string, unknown>;
  costs?: Record<string, unknown>;
  errorInfo?: Record<string, unknown>;
  traceId?: string;
  createdAt: string;
}
