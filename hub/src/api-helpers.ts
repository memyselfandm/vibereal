import type { IncomingMessage, ServerResponse } from "node:http";

export function readBody(req: IncomingMessage): Promise<string> {
  return new Promise((resolve, reject) => {
    const chunks: Buffer[] = [];
    req.on("data", (chunk) => chunks.push(chunk));
    req.on("end", () => resolve(Buffer.concat(chunks).toString("utf-8")));
    req.on("error", reject);
  });
}

export function sendJson(res: ServerResponse, status: number, data: unknown): void {
  const body = JSON.stringify(data);
  res.writeHead(status, {
    "Content-Type": "application/json",
    "Content-Length": Buffer.byteLength(body),
  });
  res.end(body);
}

export function sendError(
  res: ServerResponse,
  status: number,
  code: string,
  message: string
): void {
  sendJson(res, status, { error: { code, message } });
}

export function checkAuth(req: IncomingMessage, apiKey: string): boolean {
  if (!apiKey) return true;
  const auth = req.headers["authorization"];
  const headerToken = auth?.startsWith("Bearer ") ? auth.slice(7) : null;
  const url = new URL(req.url || "/", `http://${req.headers.host || "localhost"}`);
  const queryToken = url.searchParams.get("apiKey");
  return (headerToken || queryToken) === apiKey;
}
