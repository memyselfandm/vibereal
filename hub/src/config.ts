import { hostname } from "node:os";

export const config = {
  port: parseInt(process.env.PORT || "8080", 10),
  apiKey: process.env.HUB_API_KEY || "",
  claudePath: process.env.CLAUDE_PATH || "claude",
  processManager: process.env.PROCESS_MANAGER !== "false",
  registryUrl: process.env.REGISTRY_URL || "",
  hubName: process.env.HUB_NAME || hostname(),
  hubUrl: process.env.HUB_URL || "",
};
