# Claude Container Manager Specification

## Overview

The Claude Container Manager is responsible for spinning up, managing, and orchestrating Docker containers running Claude Code sessions. It provides isolated environments for each Claude session and connects them to the Session Hub.

## Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        Claude Container Manager                              │
│                                                                             │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                         API Server                                   │   │
│  │                    (REST + WebSocket)                                │   │
│  └───────────────────────────┬─────────────────────────────────────────┘   │
│                              │                                             │
│  ┌───────────────────────────▼─────────────────────────────────────────┐   │
│  │                     Container Orchestrator                           │   │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────────────────┐   │   │
│  │  │ Lifecycle    │  │ Health       │  │ Resource                 │   │   │
│  │  │ Manager      │  │ Monitor      │  │ Manager                  │   │   │
│  │  └──────────────┘  └──────────────┘  └──────────────────────────┘   │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                             │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                     Docker Engine API                                │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                             │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                     Running Containers                               │   │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐                  │   │
│  │  │ claude-1    │  │ claude-2    │  │ claude-3    │  ...             │   │
│  │  │ (WebSocket) │  │ (WebSocket) │  │ (WebSocket) │                  │   │
│  │  └─────────────┘  └─────────────┘  └─────────────┘                  │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Container Image

### Base Image: Claude Agent Server

Build a custom image based on [claude-agent-server](https://github.com/dzhng/claude-agent-server) with additional tooling.

```dockerfile
# Dockerfile.claude-session
FROM oven/bun:1.1-alpine AS base

# Install system dependencies
RUN apk add --no-cache \
    git \
    openssh-client \
    curl \
    jq \
    python3 \
    py3-pip \
    nodejs \
    npm

# Install common dev tools
RUN npm install -g \
    typescript \
    ts-node \
    eslint \
    prettier

WORKDIR /app

# Copy claude-agent-server
COPY claude-agent-server/ ./

# Install dependencies
RUN bun install --frozen-lockfile

# Create workspace directory
RUN mkdir -p /workspace

# Environment
ENV PORT=3000
ENV WORKSPACE_DIR=/workspace

EXPOSE 3000

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:3000/health || exit 1

CMD ["bun", "run", "start"]
```

### Container Configuration

```typescript
interface ContainerConfig {
  name: string;                        // Container name (e.g., "claude-1")
  image: string;                       // Docker image to use
  environment: {
    ANTHROPIC_API_KEY: string;
    SESSION_HUB_URL: string;
    SESSION_HUB_TOKEN: string;
    MODEL?: string;                    // Default: claude-sonnet-4-20250514
    SYSTEM_PROMPT?: string;
    MAX_TOKENS?: number;
  };
  resources: {
    memory: string;                    // e.g., "2g"
    cpus: string;                      // e.g., "1.0"
  };
  volumes: VolumeMount[];
  network: string;                     // Docker network name
}

interface VolumeMount {
  hostPath: string;
  containerPath: string;
  readOnly: boolean;
}
```

## API Specification

### REST Endpoints

#### Create Container
```http
POST /api/containers
Content-Type: application/json

{
  "name": "my-project",
  "workspacePath": "/home/user/projects/my-project",
  "model": "claude-sonnet-4-20250514",
  "systemPrompt": "You are a helpful coding assistant.",
  "resources": {
    "memory": "2g",
    "cpus": "1.0"
  }
}
```

Response:
```json
{
  "id": "container-abc123",
  "name": "my-project",
  "status": "starting",
  "wsEndpoint": "ws://localhost:3001",
  "createdAt": "2026-02-27T10:00:00Z"
}
```

#### List Containers
```http
GET /api/containers
```

Response:
```json
{
  "containers": [
    {
      "id": "container-abc123",
      "name": "my-project",
      "status": "running",
      "wsEndpoint": "ws://localhost:3001",
      "resources": {
        "memoryUsage": "512MB",
        "cpuUsage": "15%"
      },
      "uptime": "2h 15m",
      "lastActivity": "2026-02-27T10:30:00Z"
    }
  ]
}
```

#### Get Container Details
```http
GET /api/containers/:id
```

#### Stop Container
```http
POST /api/containers/:id/stop
```

#### Restart Container
```http
POST /api/containers/:id/restart
```

#### Delete Container
```http
DELETE /api/containers/:id
```

#### Get Container Logs
```http
GET /api/containers/:id/logs?tail=100&follow=false
```

#### Execute Command in Container
```http
POST /api/containers/:id/exec
Content-Type: application/json

{
  "command": ["ls", "-la", "/workspace"]
}
```

### WebSocket Events

The manager emits events to the Session Hub:

#### Container Started
```json
{
  "type": "container_started",
  "containerId": "container-abc123",
  "name": "my-project",
  "wsEndpoint": "ws://localhost:3001",
  "timestamp": "2026-02-27T10:00:00Z"
}
```

#### Container Stopped
```json
{
  "type": "container_stopped",
  "containerId": "container-abc123",
  "reason": "user_request" | "error" | "timeout",
  "timestamp": "2026-02-27T12:00:00Z"
}
```

#### Health Check Failed
```json
{
  "type": "health_check_failed",
  "containerId": "container-abc123",
  "failureCount": 3,
  "lastError": "Connection refused",
  "timestamp": "2026-02-27T10:05:00Z"
}
```

#### Resource Alert
```json
{
  "type": "resource_alert",
  "containerId": "container-abc123",
  "alertType": "memory" | "cpu" | "disk",
  "currentUsage": "95%",
  "threshold": "90%",
  "timestamp": "2026-02-27T10:10:00Z"
}
```

## Core Components

### 1. Lifecycle Manager

Handles container creation, startup, and shutdown.

```typescript
class LifecycleManager {
  // Create and start a new container
  async createContainer(config: ContainerConfig): Promise<Container>;

  // Stop a running container
  async stopContainer(containerId: string, force?: boolean): Promise<void>;

  // Restart a container
  async restartContainer(containerId: string): Promise<void>;

  // Remove a container (must be stopped)
  async removeContainer(containerId: string): Promise<void>;

  // List all managed containers
  async listContainers(): Promise<Container[]>;

  // Get container details
  async getContainer(containerId: string): Promise<Container>;
}
```

### 2. Health Monitor

Continuously monitors container health.

```typescript
class HealthMonitor {
  // Configuration
  private checkInterval = 30000;      // 30 seconds
  private unhealthyThreshold = 3;     // failures before alert
  private restartOnFailure = true;

  // Start monitoring a container
  startMonitoring(containerId: string): void;

  // Stop monitoring
  stopMonitoring(containerId: string): void;

  // Manual health check
  async checkHealth(containerId: string): Promise<HealthStatus>;

  // Events
  onHealthy(callback: (containerId: string) => void): void;
  onUnhealthy(callback: (containerId: string, error: Error) => void): void;
}

interface HealthStatus {
  healthy: boolean;
  lastCheck: Date;
  responseTime?: number;
  error?: string;
}
```

### 3. Resource Manager

Monitors and manages container resources.

```typescript
class ResourceManager {
  // Get current resource usage
  async getResourceUsage(containerId: string): Promise<ResourceUsage>;

  // Set resource limits
  async updateLimits(containerId: string, limits: ResourceLimits): Promise<void>;

  // Clean up unused resources
  async pruneContainers(olderThan: Duration): Promise<PruneResult>;
  async pruneImages(unused: boolean): Promise<PruneResult>;
  async pruneVolumes(): Promise<PruneResult>;
}

interface ResourceUsage {
  memory: {
    used: number;      // bytes
    limit: number;     // bytes
    percent: number;
  };
  cpu: {
    percent: number;
  };
  network: {
    rxBytes: number;
    txBytes: number;
  };
  disk: {
    used: number;
    limit: number;
  };
}

interface ResourceLimits {
  memory?: string;     // e.g., "4g"
  cpus?: string;       // e.g., "2.0"
  pidsLimit?: number;
}
```

## Port Management

Dynamic port allocation for container WebSocket endpoints.

```typescript
class PortManager {
  private portRange = { start: 3001, end: 3100 };
  private allocatedPorts: Map<string, number> = new Map();

  // Allocate a port for a new container
  allocate(containerId: string): number;

  // Release a port when container stops
  release(containerId: string): void;

  // Check if port is available
  isAvailable(port: number): boolean;

  // Get port for container
  getPort(containerId: string): number | undefined;
}
```

## Volume Management

### Workspace Volumes

Each container can mount a workspace from the host:

```typescript
interface WorkspaceVolume {
  // Host path to project directory
  hostPath: string;

  // Mount point inside container
  containerPath: string;  // default: /workspace

  // Read-only mode for safety
  readOnly: boolean;      // default: false
}
```

### Shared Volumes

For sharing data between containers:

```typescript
interface SharedVolume {
  name: string;           // Docker volume name
  containers: string[];   // Container IDs with access
  mountPath: string;      // Mount point in containers
}
```

## Networking

### Docker Network

All Claude containers run on a dedicated Docker network:

```bash
docker network create vibereal-network
```

### Network Configuration

```typescript
interface NetworkConfig {
  name: string;                    // "vibereal-network"
  driver: "bridge" | "overlay";
  internal: boolean;               // false - allow internet access
  subnet?: string;                 // e.g., "172.28.0.0/16"
}
```

### Container Networking

```typescript
interface ContainerNetwork {
  networkName: string;
  aliases: string[];               // DNS aliases within network
  ipAddress?: string;              // Optional static IP
}
```

## Security

### API Authentication

```typescript
interface AuthConfig {
  // API key for manager access
  apiKey: string;

  // IP allowlist (optional)
  allowedIps?: string[];

  // Rate limiting
  rateLimit: {
    windowMs: number;              // 60000 (1 minute)
    maxRequests: number;           // 100
  };
}
```

### Container Isolation

```typescript
interface SecurityConfig {
  // Run containers as non-root
  user: string;                    // "1000:1000"

  // Drop capabilities
  capDrop: string[];               // ["ALL"]

  // Add specific capabilities
  capAdd: string[];                // ["NET_BIND_SERVICE"]

  // Read-only root filesystem
  readOnlyRootfs: boolean;

  // No new privileges
  noNewPrivileges: boolean;

  // Seccomp profile
  seccompProfile: string;          // "default"
}
```

### Secrets Management

```typescript
interface SecretsConfig {
  // Anthropic API key (required)
  anthropicApiKey: {
    source: "env" | "file" | "vault";
    value?: string;                // For env source
    path?: string;                 // For file/vault source
  };

  // Additional secrets
  additionalSecrets: {
    name: string;
    source: "env" | "file" | "vault";
    value?: string;
    path?: string;
    containerEnvVar: string;       // Env var name in container
  }[];
}
```

## Configuration

### Manager Configuration File

```yaml
# config.yaml
server:
  port: 8081
  host: "0.0.0.0"

docker:
  socketPath: "/var/run/docker.sock"
  network: "vibereal-network"

containers:
  image: "vibereal/claude-session:latest"
  defaultResources:
    memory: "2g"
    cpus: "1.0"
  maxContainers: 10

ports:
  range:
    start: 3001
    end: 3100

health:
  checkInterval: 30000
  unhealthyThreshold: 3
  restartOnFailure: true

sessionHub:
  url: "ws://session-hub:8080"
  token: "${SESSION_HUB_TOKEN}"

security:
  apiKey: "${MANAGER_API_KEY}"
  containerUser: "1000:1000"
  readOnlyRootfs: false

logging:
  level: "info"
  format: "json"
```

### Environment Variables

```bash
# Required
ANTHROPIC_API_KEY=sk-ant-...
SESSION_HUB_URL=ws://session-hub:8080
SESSION_HUB_TOKEN=your-token
MANAGER_API_KEY=your-api-key

# Optional
DOCKER_HOST=unix:///var/run/docker.sock
MAX_CONTAINERS=10
DEFAULT_MODEL=claude-sonnet-4-20250514
LOG_LEVEL=info
```

## Deployment

### Docker Compose

```yaml
version: "3.8"

services:
  container-manager:
    build:
      context: .
      dockerfile: Dockerfile.manager
    ports:
      - "8081:8081"
    environment:
      - ANTHROPIC_API_KEY=${ANTHROPIC_API_KEY}
      - SESSION_HUB_URL=ws://session-hub:8080
      - SESSION_HUB_TOKEN=${SESSION_HUB_TOKEN}
      - MANAGER_API_KEY=${MANAGER_API_KEY}
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
      - /home/user/projects:/projects:ro
    networks:
      - vibereal-network
    depends_on:
      - session-hub

networks:
  vibereal-network:
    driver: bridge
```

### Manager Dockerfile

```dockerfile
# Dockerfile.manager
FROM node:20-alpine

RUN apk add --no-cache docker-cli

WORKDIR /app

COPY package*.json ./
RUN npm ci --only=production

COPY . .

EXPOSE 8081

CMD ["node", "dist/index.js"]
```

## CLI Tool

Optional CLI for manual container management:

```bash
# List containers
vibereal containers list

# Create new container
vibereal containers create \
  --name my-project \
  --workspace /path/to/project \
  --model claude-sonnet-4-20250514

# Stop container
vibereal containers stop container-abc123

# View logs
vibereal containers logs container-abc123 --follow

# Shell into container
vibereal containers exec container-abc123 /bin/sh

# Resource usage
vibereal containers stats
```

## Monitoring & Metrics

### Prometheus Metrics

```
# Container counts
vibereal_containers_total{status="running"} 3
vibereal_containers_total{status="stopped"} 1

# Resource usage
vibereal_container_memory_bytes{container="claude-1"} 536870912
vibereal_container_cpu_percent{container="claude-1"} 15.5

# Health checks
vibereal_health_checks_total{container="claude-1", result="success"} 150
vibereal_health_checks_total{container="claude-1", result="failure"} 2

# API requests
vibereal_api_requests_total{endpoint="/containers", method="POST"} 10
vibereal_api_request_duration_seconds{endpoint="/containers"} 0.5
```

### Health Endpoints

```
GET /health           # Manager health
GET /health/docker    # Docker daemon connectivity
GET /metrics          # Prometheus metrics
```

## Error Handling

### Error Codes

| Code | Description |
|------|-------------|
| `CONTAINER_NOT_FOUND` | Container ID does not exist |
| `CONTAINER_ALREADY_EXISTS` | Container name already in use |
| `CONTAINER_START_FAILED` | Failed to start container |
| `DOCKER_UNAVAILABLE` | Cannot connect to Docker daemon |
| `RESOURCE_LIMIT_EXCEEDED` | Max containers reached |
| `PORT_EXHAUSTED` | No available ports |
| `INVALID_CONFIG` | Invalid container configuration |
| `HEALTH_CHECK_FAILED` | Container is unhealthy |

### Recovery Strategies

| Scenario | Action |
|----------|--------|
| Container crash | Auto-restart with backoff |
| Health check failure (3x) | Restart container, notify user |
| Port conflict | Reallocate port, restart |
| OOM kill | Increase memory limit, restart |
| Docker daemon restart | Re-register existing containers |

## Future Considerations

1. **Kubernetes Support**: Deploy containers to K8s cluster
2. **Remote Docker Hosts**: Manage containers across multiple servers
3. **Container Templates**: Pre-configured environments (Node, Python, etc.)
4. **Snapshot/Restore**: Save and restore container state
5. **GPU Support**: Enable GPU access for ML workloads
6. **Cost Tracking**: Track API usage per container
