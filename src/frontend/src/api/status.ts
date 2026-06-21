export type ProviderLastOutcome = "not_started" | "success" | "error" | "timeout" | "cancelled";
export type AdapterLastOutcome = "not_started" | "success" | "error" | "timeout" | "cancelled" | "disabled";

export interface ProviderStatusResponse {
  providerType: string;
  model: string;
  isConfigured: boolean;
  isReady: boolean;
  lastOutcome: string;
  lastErrorCategory: string | null;
  lastDurationMs: number | null;
  lastUpdatedAtUtc: string | null;
}

export interface AdapterStatusResponse {
  adapterType: string;
  isConfigured: boolean;
  isReady: boolean;
  lastOutcome: string;
  lastErrorCategory: string | null;
  lastDurationMs: number | null;
  lastUpdatedAtUtc: string | null;
}

export interface RuntimeStatusResult {
  provider: ProviderStatusResponse;
  adapter: AdapterStatusResponse;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}

function requireStringField(record: Record<string, unknown>, fieldName: string): string {
  const value = record[fieldName];
  if (typeof value !== "string" || value.trim().length === 0) {
    throw new Error("Runtime status API returned an invalid response.");
  }

  return value;
}

function requireBooleanField(record: Record<string, unknown>, fieldName: string): boolean {
  const value = record[fieldName];
  if (typeof value !== "boolean") {
    throw new Error("Runtime status API returned an invalid response.");
  }

  return value;
}

function requireNullableStringField(record: Record<string, unknown>, fieldName: string): string | null {
  const value = record[fieldName];
  if (value === null || value === undefined) {
    return null;
  }
  if (typeof value !== "string") {
    throw new Error("Runtime status API returned an invalid response.");
  }
  return value;
}

function requireNullableNumberField(record: Record<string, unknown>, fieldName: string): number | null {
  const value = record[fieldName];
  if (value === null || value === undefined) {
    return null;
  }
  if (typeof value !== "number" || !Number.isFinite(value)) {
    throw new Error("Runtime status API returned an invalid response.");
  }
  return value;
}

function parseProviderStatus(value: unknown): ProviderStatusResponse {
  if (!isRecord(value)) {
    throw new Error("Runtime status API returned an invalid response.");
  }

  return {
    providerType: requireStringField(value, "providerType"),
    model: requireStringField(value, "model"),
    isConfigured: requireBooleanField(value, "isConfigured"),
    isReady: requireBooleanField(value, "isReady"),
    lastOutcome: requireStringField(value, "lastOutcome"),
    lastErrorCategory: requireNullableStringField(value, "lastErrorCategory"),
    lastDurationMs: requireNullableNumberField(value, "lastDurationMs"),
    lastUpdatedAtUtc: requireNullableStringField(value, "lastUpdatedAtUtc"),
  };
}

function parseAdapterStatus(value: unknown): AdapterStatusResponse {
  if (!isRecord(value)) {
    throw new Error("Runtime status API returned an invalid response.");
  }

  return {
    adapterType: requireStringField(value, "adapterType"),
    isConfigured: requireBooleanField(value, "isConfigured"),
    isReady: requireBooleanField(value, "isReady"),
    lastOutcome: requireStringField(value, "lastOutcome"),
    lastErrorCategory: requireNullableStringField(value, "lastErrorCategory"),
    lastDurationMs: requireNullableNumberField(value, "lastDurationMs"),
    lastUpdatedAtUtc: requireNullableStringField(value, "lastUpdatedAtUtc"),
  };
}

export async function fetchProviderStatus(signal?: AbortSignal): Promise<ProviderStatusResponse> {
  const response = await fetch("/api/provider/status", {
    signal,
    headers: { Accept: "application/json" },
  });

  if (!response.ok) {
    throw new Error("Provider status is unavailable right now.");
  }

  const body = await response.json().catch(() => {
    throw new Error("Provider status API returned an invalid response.");
  });

  return parseProviderStatus(body);
}

export async function fetchAdapterStatus(signal?: AbortSignal): Promise<AdapterStatusResponse> {
  const response = await fetch("/api/dwarves/adapter-status", {
    signal,
    headers: { Accept: "application/json" },
  });

  if (!response.ok) {
    throw new Error("Adapter status is unavailable right now.");
  }

  const body = await response.json().catch(() => {
    throw new Error("Adapter status API returned an invalid response.");
  });

  return parseAdapterStatus(body);
}

export async function fetchRuntimeStatus(signal?: AbortSignal): Promise<RuntimeStatusResult> {
  const [provider, adapter] = await Promise.all([
    fetchProviderStatus(signal),
    fetchAdapterStatus(signal),
  ]);
  return { provider, adapter };
}
