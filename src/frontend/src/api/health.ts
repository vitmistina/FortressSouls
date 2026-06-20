export interface HealthResponse {
  status: string;
  version: string;
  adapter: string;
  provider: string;
  observability: string;
}

export interface HealthResult {
  health: HealthResponse;
  correlationId?: string;
}

const correlationIdPattern = /^[A-Za-z0-9_.-]{1,64}$/;

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}

function requireStringField(record: Record<string, unknown>, fieldName: string): string {
  const value = record[fieldName];
  if (typeof value !== "string" || value.trim().length === 0) {
    throw new Error("Backend health returned an invalid response.");
  }

  return value;
}

function parseHealthResponse(value: unknown): HealthResponse {
  if (!isRecord(value)) {
    throw new Error("Backend health returned an invalid response.");
  }

  return {
    status: requireStringField(value, "status"),
    version: requireStringField(value, "version"),
    adapter: requireStringField(value, "adapter"),
    provider: requireStringField(value, "provider"),
    observability: requireStringField(value, "observability"),
  };
}

function sanitizeCorrelationId(value: string | null): string | undefined {
  if (value && correlationIdPattern.test(value)) {
    return value;
  }

  return undefined;
}

export async function fetchHealth(signal?: AbortSignal): Promise<HealthResult> {
  const response = await fetch("/api/health", {
    signal,
    headers: {
      Accept: "application/json",
    },
  });

  const correlationId = sanitizeCorrelationId(response.headers.get("X-Correlation-ID"));

  if (!response.ok) {
    throw new Error("Backend health is unavailable right now.");
  }

  const body = await response.json().catch(() => {
    throw new Error("Backend health returned an invalid response.");
  });

  return {
    health: parseHealthResponse(body),
    correlationId,
  };
}
