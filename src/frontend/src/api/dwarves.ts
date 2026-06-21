export interface DwarfListItem {
  id: string;
  displayName: string;
  profession: string;
  currentJob: string | null;
  stressLevel: string;
}

export interface DwarfListSource {
  adapter: string;
  snapshotTick: number;
  schemaVersion: string;
}

export interface DwarfListResponse {
  items: DwarfListItem[];
  source: DwarfListSource;
}

export interface DwarfListResult {
  list: DwarfListResponse;
  correlationId?: string;
}

export interface DwarfSnapshotIdentity {
  displayName: string;
  profession: string;
}

export interface DwarfSnapshotWork {
  currentJob: string | null;
  labors: string[];
}

export interface DwarfSkill {
  name: string;
  level: number;
  description: string;
}

export interface DwarfPersonalityTrait {
  name: string;
  rawValue: number;
  interpretation: string;
}

export interface DwarfValue {
  name: string;
  rawValue: number;
  interpretation: string;
}

export interface DwarfPersonality {
  traits: DwarfPersonalityTrait[];
  values: DwarfValue[];
}

export interface DwarfNeedSummary {
  name: string;
  summary: string;
}

export interface DwarfRelationship {
  type: string;
  displayName: string;
}

export interface DwarfHealth {
  summary: string;
}

export interface DwarfSnapshotDebug {
  adapter: string;
  rawAvailable: boolean;
}

export interface DwarfSnapshotResponse {
  schemaVersion: string;
  dwarfId: string;
  extractedAt: string;
  gameTick: number;
  identity: DwarfSnapshotIdentity;
  work: DwarfSnapshotWork;
  skills: DwarfSkill[];
  personality: DwarfPersonality;
  needs: DwarfNeedSummary[];
  relationships: DwarfRelationship[];
  health: DwarfHealth;
  debug: DwarfSnapshotDebug;
}

export interface DwarfSnapshotResult {
  snapshot: DwarfSnapshotResponse;
  correlationId?: string;
}

interface ApiErrorBody {
  errorCode?: string;
}

const correlationIdPattern = /^[A-Za-z0-9_.-]{1,64}$/;

export class DwarfApiError extends Error {
  readonly statusCode?: number;
  readonly errorCode?: string;
  readonly correlationId?: string;

  constructor(message: string, statusCode?: number, errorCode?: string, correlationId?: string) {
    super(message);
    this.name = "DwarfApiError";
    this.statusCode = statusCode;
    this.errorCode = errorCode;
    this.correlationId = correlationId;
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}

function requireStringField(record: Record<string, unknown>, fieldName: string): string {
  const value = record[fieldName];
  if (typeof value !== "string" || value.trim().length === 0) {
    throw new Error("Backend dwarf API returned an invalid response.");
  }

  return value;
}

function requireNullableStringField(record: Record<string, unknown>, fieldName: string): string | null {
  const value = record[fieldName];
  if (value === null) {
    return null;
  }
  if (typeof value !== "string") {
    throw new Error("Backend dwarf API returned an invalid response.");
  }
  return value;
}

function requireNumberField(record: Record<string, unknown>, fieldName: string): number {
  const value = record[fieldName];
  if (typeof value !== "number" || !Number.isFinite(value)) {
    throw new Error("Backend dwarf API returned an invalid response.");
  }

  return value;
}

function requireBooleanField(record: Record<string, unknown>, fieldName: string): boolean {
  const value = record[fieldName];
  if (typeof value !== "boolean") {
    throw new Error("Backend dwarf API returned an invalid response.");
  }

  return value;
}

function requireArrayField(record: Record<string, unknown>, fieldName: string): unknown[] {
  const value = record[fieldName];
  if (!Array.isArray(value)) {
    throw new Error("Backend dwarf API returned an invalid response.");
  }

  return value;
}

function sanitizeCorrelationId(value: string | null): string | undefined {
  if (value && correlationIdPattern.test(value)) {
    return value;
  }
  return undefined;
}

function parseListItem(value: unknown): DwarfListItem {
  if (!isRecord(value)) {
    throw new Error("Backend dwarf API returned an invalid response.");
  }

  return {
    id: requireStringField(value, "id"),
    displayName: requireStringField(value, "displayName"),
    profession: requireStringField(value, "profession"),
    currentJob: requireNullableStringField(value, "currentJob"),
    stressLevel: requireStringField(value, "stressLevel"),
  };
}

function parseListSource(value: unknown): DwarfListSource {
  if (!isRecord(value)) {
    throw new Error("Backend dwarf API returned an invalid response.");
  }

  return {
    adapter: requireStringField(value, "adapter"),
    snapshotTick: requireNumberField(value, "snapshotTick"),
    schemaVersion: requireStringField(value, "schemaVersion"),
  };
}

function parseDwarfListResponse(value: unknown): DwarfListResponse {
  if (!isRecord(value)) {
    throw new Error("Backend dwarf API returned an invalid response.");
  }

  const rawItems = requireArrayField(value, "items");

  return {
    items: rawItems.map(parseListItem),
    source: parseListSource(value.source),
  };
}

function parseSkill(value: unknown): DwarfSkill {
  if (!isRecord(value)) {
    throw new Error("Backend dwarf API returned an invalid response.");
  }

  return {
    name: requireStringField(value, "name"),
    level: requireNumberField(value, "level"),
    description: requireStringField(value, "description"),
  };
}

function parsePersonalityTrait(value: unknown): DwarfPersonalityTrait {
  if (!isRecord(value)) {
    throw new Error("Backend dwarf API returned an invalid response.");
  }

  return {
    name: requireStringField(value, "name"),
    rawValue: requireNumberField(value, "rawValue"),
    interpretation: requireStringField(value, "interpretation"),
  };
}

function parseValue(value: unknown): DwarfValue {
  if (!isRecord(value)) {
    throw new Error("Backend dwarf API returned an invalid response.");
  }

  return {
    name: requireStringField(value, "name"),
    rawValue: requireNumberField(value, "rawValue"),
    interpretation: requireStringField(value, "interpretation"),
  };
}

function parseNeed(value: unknown): DwarfNeedSummary {
  if (!isRecord(value)) {
    throw new Error("Backend dwarf API returned an invalid response.");
  }

  return {
    name: requireStringField(value, "name"),
    summary: requireStringField(value, "summary"),
  };
}

function parseRelationship(value: unknown): DwarfRelationship {
  if (!isRecord(value)) {
    throw new Error("Backend dwarf API returned an invalid response.");
  }

  return {
    type: requireStringField(value, "type"),
    displayName: requireStringField(value, "displayName"),
  };
}

function parseSnapshotResponse(value: unknown): DwarfSnapshotResponse {
  if (!isRecord(value)) {
    throw new Error("Backend dwarf API returned an invalid response.");
  }

  if (!isRecord(value.identity) || !isRecord(value.work) || !isRecord(value.personality) || !isRecord(value.health) || !isRecord(value.debug)) {
    throw new Error("Backend dwarf API returned an invalid response.");
  }

  return {
    schemaVersion: requireStringField(value, "schemaVersion"),
    dwarfId: requireStringField(value, "dwarfId"),
    extractedAt: requireStringField(value, "extractedAt"),
    gameTick: requireNumberField(value, "gameTick"),
    identity: {
      displayName: requireStringField(value.identity, "displayName"),
      profession: requireStringField(value.identity, "profession"),
    },
    work: {
      currentJob: requireNullableStringField(value.work, "currentJob"),
      labors: requireArrayField(value.work, "labors").map((labor) => {
        if (typeof labor !== "string") {
          throw new Error("Backend dwarf API returned an invalid response.");
        }
        return labor;
      }),
    },
    skills: requireArrayField(value, "skills").map(parseSkill),
    personality: {
      traits: requireArrayField(value.personality, "traits").map(parsePersonalityTrait),
      values: requireArrayField(value.personality, "values").map(parseValue),
    },
    needs: requireArrayField(value, "needs").map(parseNeed),
    relationships: requireArrayField(value, "relationships").map(parseRelationship),
    health: {
      summary: requireStringField(value.health, "summary"),
    },
    debug: {
      adapter: requireStringField(value.debug, "adapter"),
      rawAvailable: requireBooleanField(value.debug, "rawAvailable"),
    },
  };
}

async function parseApiErrorBody(response: Response): Promise<ApiErrorBody | undefined> {
  const body = await response.json().catch(() => undefined);
  if (!isRecord(body)) {
    return undefined;
  }

  const errorCode = body.errorCode;
  if (typeof errorCode !== "string" || errorCode.trim().length === 0) {
    return undefined;
  }

  return { errorCode };
}

export async function fetchDwarfList(signal?: AbortSignal): Promise<DwarfListResult> {
  const response = await fetch("/api/dwarves", {
    signal,
    headers: {
      Accept: "application/json",
    },
  });

  const correlationId = sanitizeCorrelationId(response.headers.get("X-Correlation-ID"));

  if (!response.ok) {
    const apiError = await parseApiErrorBody(response);
    throw new DwarfApiError(
      "Dwarf roster is unavailable right now.",
      response.status,
      apiError?.errorCode,
      correlationId,
    );
  }

  const body = await response.json().catch(() => {
    throw new Error("Backend dwarf API returned an invalid response.");
  });

  return {
    list: parseDwarfListResponse(body),
    correlationId,
  };
}

export async function fetchDwarfSnapshot(
  dwarfId: string,
  signal?: AbortSignal,
): Promise<DwarfSnapshotResult> {
  const response = await fetch(`/api/dwarves/${encodeURIComponent(dwarfId)}/snapshot`, {
    signal,
    headers: {
      Accept: "application/json",
    },
  });

  const correlationId = sanitizeCorrelationId(response.headers.get("X-Correlation-ID"));

  if (!response.ok) {
    const apiError = await parseApiErrorBody(response);
    throw new DwarfApiError(
      "Unable to load dwarf snapshot right now.",
      response.status,
      apiError?.errorCode,
      correlationId,
    );
  }

  const body = await response.json().catch(() => {
    throw new Error("Backend dwarf API returned an invalid response.");
  });

  return {
    snapshot: parseSnapshotResponse(body),
    correlationId,
  };
}
