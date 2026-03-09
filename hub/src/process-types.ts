import type { ChildProcess } from "node:child_process";

export interface ManagedProcess {
  id: string;
  sessionId: string;
  process: ChildProcess;
  createdAt: Date;
  prompt: string;
  workingDirectory: string;
}

export interface CreateSessionRequest {
  prompt: string;
  workingDirectory?: string;
  name?: string;
  allowedTools?: string[];
  permissionMode?: string;
}

export interface CreateSessionResponse {
  sessionId: string;
  name: string;
  status: string;
}
