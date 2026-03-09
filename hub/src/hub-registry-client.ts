import { request as httpRequest } from "node:http";
import { request as httpsRequest } from "node:https";
import { config } from "./config.ts";
import { hostname } from "node:os";

let heartbeatInterval: ReturnType<typeof setInterval> | null = null;
let registeredId: string | null = null;
let registryUrl: string = "";

export function start(): void {
  // Read env at call time so tests can set env before calling start()
  const effectiveRegistryUrl = process.env.REGISTRY_URL || config.registryUrl;
  if (!effectiveRegistryUrl) {
    console.log("[registry-client] No REGISTRY_URL set, skipping registration");
    return;
  }

  registryUrl = effectiveRegistryUrl;
  const hubInfo = getHubInfo();

  console.log(`[registry-client] Registering with registry at ${registryUrl}`);
  console.log(`[registry-client] Hub: ${hubInfo.name} (${hubInfo.url})`);

  // Initial registration
  registerHub(hubInfo);

  // Heartbeat every 30 seconds
  heartbeatInterval = setInterval(() => {
    registerHub(hubInfo);
  }, 30_000);
  heartbeatInterval.unref();
}

export async function stop(): Promise<void> {
  if (heartbeatInterval) {
    clearInterval(heartbeatInterval);
    heartbeatInterval = null;
  }

  // Deregister from registry
  if (registeredId && registryUrl) {
    console.log(`[registry-client] Deregistering from registry`);
    try {
      await makeRequest("DELETE", `${registryUrl}/api/registry/hubs/${registeredId}`);
    } catch (err: any) {
      console.warn(`[registry-client] Failed to deregister: ${err.message}`);
    }
    registeredId = null;
    registryUrl = "";
  }
}

function getHubInfo() {
  // Read env at call time so runtime overrides (e.g. in tests) are picked up
  return {
    name: process.env.HUB_NAME || config.hubName || hostname(),
    url: process.env.HUB_URL || config.hubUrl || `http://localhost:${config.port}`,
    capabilities: config.processManager ? ["relay", "spawn"] : ["relay"],
  };
}

function registerHub(hubInfo: { name: string; url: string; capabilities: string[] }): void {
  const body = JSON.stringify(hubInfo);
  const endpoint = `${registryUrl}/api/registry/hubs`;

  makeRequest("POST", endpoint, body)
    .then((data) => {
      if (data.id) {
        registeredId = data.id;
      }
    })
    .catch((err) => {
      console.warn(`[registry-client] Registration failed: ${err.message}`);
    });
}

function makeRequest(method: string, url: string, body?: string): Promise<any> {
  return new Promise((resolve, reject) => {
    const parsed = new URL(url);
    const isHttps = parsed.protocol === "https:";
    const doRequest = isHttps ? httpsRequest : httpRequest;

    const options = {
      hostname: parsed.hostname,
      port: parsed.port || (isHttps ? 443 : 80),
      path: parsed.pathname + parsed.search,
      method,
      headers: {
        "Content-Type": "application/json",
        ...(body ? { "Content-Length": Buffer.byteLength(body) } : {}),
        ...(config.apiKey ? { Authorization: `Bearer ${config.apiKey}` } : {}),
      },
    };

    const req = doRequest(options, (res) => {
      const chunks: Buffer[] = [];
      res.on("data", (chunk) => chunks.push(chunk));
      res.on("end", () => {
        const text = Buffer.concat(chunks).toString("utf-8");
        try {
          resolve(JSON.parse(text));
        } catch {
          resolve({ raw: text });
        }
      });
    });

    req.on("error", reject);
    req.setTimeout(10_000, () => {
      req.destroy(new Error("Request timeout"));
    });

    if (body) {
      req.write(body);
    }
    req.end();
  });
}
