import type {
  Agent,
  AgentLife,
  Autopsy,
  Decision,
  EventDetail,
  Group,
  Health,
  Location,
  Memory,
  Relationship,
  Settlement,
  SimulationEvent,
  World,
  WorldHistoryEntry,
} from './types';

export const API_BASE: string =
  (import.meta.env.VITE_API_URL as string | undefined) ?? '/api';

export const HUB_URL: string =
  (import.meta.env.VITE_HUB_URL as string | undefined) ?? '/hubs/simulation';

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${API_BASE}${path}`, {
    headers: {
      'Content-Type': 'application/json',
      ...(init?.headers ?? {}),
    },
    ...init,
  });

  if (!response.ok) {
    let detail = response.statusText;
    try {
      const body = await response.json();
      detail = JSON.stringify(body);
    } catch {
    }
    throw new Error(`${response.status} ${detail}`);
  }

  return response.json() as Promise<T>;
}

function qs(params: Record<string, string | number | boolean | undefined>): string {
  const entries = Object.entries(params).filter(([, v]) => v !== undefined);
  if (entries.length === 0) return '';
  return '?' + entries.map(([k, v]) => `${k}=${encodeURIComponent(String(v))}`).join('&');
}

export const api = {
  health: () => fetch(`${API_BASE.replace(/\/api$/, '')}/health`).then((r) => r.json() as Promise<Health>),
  listWorlds: () => request<World[]>('/worlds'),
  getWorld: (id: string) => request<World>(`/worlds/${id}`),
  createWorld: (name: string, population?: number) =>
    request<World>('/worlds', {
      method: 'POST',
      body: JSON.stringify({ name, initialPopulation: population }),
    }),
  startWorld: (id: string) => request<World>(`/worlds/${id}/start`, { method: 'POST' }),
  pauseWorld: (id: string) => request<World>(`/worlds/${id}/pause`, { method: 'POST' }),
  setSpeed: (id: string, speed: number) =>
    request<World>(`/worlds/${id}/speed`, {
      method: 'POST',
      body: JSON.stringify({ speed }),
    }),

  listAgents: (worldId: string, aliveOnly = true, limit = 500) =>
    request<Agent[]>(`/worlds/${worldId}/agents${qs({ aliveOnly, limit })}`),
  getAgent: (agentId: string) => request<Agent>(`/agents/${agentId}`),
  getAgentRelationships: (agentId: string, direction: 'outgoing' | 'incoming' | 'both' = 'outgoing') =>
    request<Relationship[]>(`/agents/${agentId}/relationships${qs({ direction })}`),
  getAgentMemories: (agentId: string, limit = 30) =>
    request<Memory[]>(`/agents/${agentId}/memories${qs({ limit })}`),
  getAgentDecisions: (agentId: string, limit = 5) =>
    request<Decision[]>(`/agents/${agentId}/decisions${qs({ limit })}`),

  listLocations: (worldId: string) => request<Location[]>(`/worlds/${worldId}/locations`),
  listEvents: (worldId: string, opts: { limit?: number; sinceTick?: number; beforeTick?: number; eventType?: string } = {}) =>
    request<SimulationEvent[]>(`/worlds/${worldId}/events${qs(opts)}`),
  listSettlements: (worldId: string) => request<Settlement[]>(`/worlds/${worldId}/settlements`),
  listGroups: (worldId: string) => request<Group[]>(`/worlds/${worldId}/groups`),

  getEventDetail: (eventId: string) => request<EventDetail>(`/events/${eventId}`),
  getAgentLife: (agentId: string) => request<AgentLife>(`/agents/${agentId}/life`),
  getWorldHistory: (worldId: string, opts: { limit?: number; minImportance?: number } = {}) =>
    request<WorldHistoryEntry[]>(`/worlds/${worldId}/history${qs(opts)}`),
  getAutopsy: (worldId: string, subject: string) =>
    request<Autopsy>(`/worlds/${worldId}/autopsy${qs({ subject })}`),
};
