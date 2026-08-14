import { create } from 'zustand';
import type { EventView } from '../lib/format';
import type { Agent, World } from '../api/types';

interface SimState {
  world: World | null;
  setWorld: (world: World) => void;

  selectedAgentId: string | null;
  followedAgentId: string | null;
  agentMap: Map<string, Agent>;
  selectAgent: (id: string | null) => void;
  followAgent: (id: string | null) => void;
  setAgents: (agents: Agent[]) => void;

  selectedEventId: string | null;
  selectEvent: (id: string | null) => void;

  liveEvents: EventView[];
  eventsSinceTick: number;
  pushEvents: (events: EventView[]) => void;
  prependHistory: (events: EventView[]) => void;

  eventFilter: string;
  setEventFilter: (filter: string) => void;

  debugMode: boolean;
  setDebugMode: (on: boolean) => void;

  reset: () => void;
}

const MAX_LIVE_EVENTS = 150;

export const useSimStore = create<SimState>((set) => ({
  world: null,
  setWorld: (world) => set({ world }),

  selectedAgentId: null,
  followedAgentId: null,
  agentMap: new Map(),
  selectAgent: (id) => set({ selectedAgentId: id, selectedEventId: null }),
  followAgent: (id) => set({ followedAgentId: id, selectedAgentId: id }),
  setAgents: (agents) =>
    set({ agentMap: new Map(agents.map((a) => [a.id, a])) }),

  selectedEventId: null,
  selectEvent: (id) => set({ selectedEventId: id, selectedAgentId: null }),

  liveEvents: [],
  eventsSinceTick: 0,
  pushEvents: (events) =>
    set((s) => {
      if (events.length === 0) return s;
      const merged = [...events, ...s.liveEvents].slice(0, MAX_LIVE_EVENTS);
      const maxTick = events.reduce((m, e) => Math.max(m, e.tick), s.eventsSinceTick);
      return { liveEvents: merged, eventsSinceTick: maxTick };
    }),
  prependHistory: (events) =>
    set((s) => {
      const existing = new Set(s.liveEvents.map((e) => e.id));
      const fresh = events.filter((e) => !existing.has(e.id));
      if (fresh.length === 0) return s;
      return { liveEvents: [...s.liveEvents, ...fresh].slice(0, MAX_LIVE_EVENTS) };
    }),

  eventFilter: 'all',
  setEventFilter: (filter) => set({ eventFilter: filter }),

  debugMode: false,
  setDebugMode: (on) => set({ debugMode: on }),

  reset: () =>
    set({
      world: null,
      selectedAgentId: null,
      followedAgentId: null,
      agentMap: new Map(),
      selectedEventId: null,
      liveEvents: [],
      eventsSinceTick: 0,
      eventFilter: 'all',
      debugMode: false,
    }),
}));
