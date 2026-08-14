import type { Agent, SignalEvent, SimulationEvent } from '../api/types';

export type EventCategory =
  | 'all'
  | 'social'
  | 'economy'
  | 'death'
  | 'birth'
  | 'conflict'
  | 'settlement'
  | 'movement'
  | 'misc';

const EVENT_CATEGORY: Record<string, EventCategory> = {
  AgentTalked: 'social',
  AgentHelped: 'social',
  AgentSharedFood: 'social',
  AgentTraded: 'social',
  AgentWorked: 'economy',
  AgentHarvestedFood: 'economy',
  AgentGatheredWood: 'economy',
  AgentAte: 'economy',
  AgentDied: 'death',
  AgentBorn: 'birth',
  ConflictOccurred: 'conflict',
  AgentStole: 'conflict',
  SettlementFormed: 'settlement',
  GroupFormed: 'settlement',
  AgentMoved: 'movement',
  AgentRested: 'misc',
  AgentInsulted: 'conflict',
};

export function categoryOf(eventType: string): EventCategory {
  return EVENT_CATEGORY[eventType] ?? 'misc';
}

export const CATEGORY_LABELS: Record<Exclude<EventCategory, 'all'>, string> = {
  social: 'Social',
  economy: 'Economy',
  death: 'Death',
  birth: 'Birth',
  conflict: 'Conflict',
  settlement: 'Settlement',
  movement: 'Movement',
  misc: 'Other',
};

export interface EventView {
  id: string;
  tick: number;
  simulationTime: string;
  eventType: string;
  category: EventCategory;
  actorAgentId: string | null;
  targetAgentId: string | null;
  data: Record<string, unknown>;
  importance: number;
  text: string;
}

function fmtName(id: string | null | undefined, agents: Map<string, Agent>): string | null {
  if (!id) return null;
  return agents.get(id)?.name ?? null;
}

export function toEventView(
  evt: SignalEvent | SimulationEvent,
  agents: Map<string, Agent>,
): EventView {
  const data =
    typeof evt.data === 'string'
      ? safeParse((evt as SimulationEvent).data)
      : (evt.data as Record<string, unknown>);

  const actorName = fmtName(evt.actorAgentId, agents) ?? shortId(evt.actorAgentId);
  const targetName = fmtName(evt.targetAgentId, agents) ?? (data.targetName as string) ?? shortId(evt.targetAgentId);

  return {
    id: 'id' in evt ? evt.id : `${evt.tick}-${evt.eventType}-${Math.random().toString(36).slice(2, 8)}`,
    tick: evt.tick,
    simulationTime: evt.simulationTime,
    eventType: evt.eventType,
    category: categoryOf(evt.eventType),
    actorAgentId: evt.actorAgentId,
    targetAgentId: evt.targetAgentId,
    data,
    importance: 'importance' in evt ? (evt.importance as number) : 1,
    text: describeEvent(evt.eventType, actorName, targetName, data),
  };
}

function describeEvent(
  type: string,
  actorName: string | null,
  targetName: string | null,
  data: Record<string, unknown>,
): string {
  switch (type) {
    case 'AgentBorn':
      return `${actorName ?? 'An agent'} was born`;
    case 'AgentDied':
      return `${actorName ?? 'An agent'} died of ${String(data.cause ?? 'unknown')} at age ${num(data.age)}`;
    case 'AgentAte':
      return `${actorName} ate food (hunger ${num(data.hungerAfter)})`;
    case 'AgentRested':
      return `${actorName} rested (energy ${num(data.energyAfter)})`;
    case 'AgentMoved':
      return `${actorName} moved to ${String(data.to ?? '?')}`;
    case 'AgentHarvestedFood':
      return `${actorName} harvested ${num(data.amount)} food at the farm`;
    case 'AgentGatheredWood':
      return `${actorName} gathered ${num(data.amount)} wood (foraged ${num(data.foodForaged)} food)`;
    case 'AgentWorked':
      return `${actorName} worked, earning ${num(data.moneyEarned)} money and ${num(data.foodEarned)} food`;
    case 'AgentTalked':
      return `${actorName} talked with ${targetName ?? 'someone'}`;
    case 'AgentHelped':
      return `${actorName} helped ${targetName ?? 'someone'} with food`;
    case 'AgentSharedFood':
      return `${actorName} shared food with ${targetName ?? 'someone'}`;
    case 'AgentTraded':
      return `${actorName} sold ${num(data.quantity)} ${String(data.resourceType ?? 'resource')} to ${targetName} for ${num(data.totalPrice)} money`;
    case 'AgentStole':
      return `${actorName} stole ${num(data.amount)} food from ${targetName ?? 'someone'}`;
    case 'AgentInsulted':
      return `${actorName} insulted ${targetName ?? 'someone'}`;
    case 'ConflictOccurred':
      return `Conflict: ${actorName} vs ${targetName} (${Array.isArray(data.causes) ? (data.causes as string[]).join(', ') : 'unknown cause'})`;
    case 'SettlementFormed':
      return `Settlement ${String(data.name ?? '?')} formed at ${String(data.location ?? '?')} (${num(data.population)} residents)`;
    case 'GroupFormed':
      return `${actorName ?? ''}Group ${String(data.name ?? '?')} (${String(data.type ?? '?')}) formed with ${num(data.memberCount)} members`;
    default:
      return `${type}: ${JSON.stringify(data).slice(0, 80)}`;
  }
}

function safeParse(json: string): Record<string, unknown> {
  try {
    return JSON.parse(json) as Record<string, unknown>;
  } catch {
    return {};
  }
}

function num(v: unknown): string {
  if (typeof v === 'number') return Number.isInteger(v) ? String(v) : v.toFixed(2);
  return String(v ?? '');
}

function shortId(id: string | null | undefined): string {
  if (!id) return 'someone';
  return `#${id.slice(0, 4)}`;
}

export function formatSimTime(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toISOString().slice(0, 19).replace('T', ' ');
}

export function formatSimDate(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  const year = d.getUTCFullYear();
  const month = String(d.getUTCMonth() + 1).padStart(2, '0');
  const day = String(d.getUTCDate()).padStart(2, '0');
  const hour = String(d.getUTCHours()).padStart(2, '0');
  const min = String(d.getUTCMinutes()).padStart(2, '0');
  return `${year}-${month}-${day} ${hour}:${min}`;
}

export function worldAgeDays(worldCreatedAt: string, currentSimTime: string): string {
  const start = new Date(worldCreatedAt).getTime();
  const now = new Date(currentSimTime).getTime();
  if (Number.isNaN(start) || Number.isNaN(now)) return '—';
  const days = (now - start) / 86_400_000;
  if (days < 1) return `${Math.round(days * 24)}h`;
  return `${Math.round(days)}d`;
}

export function interpretTrait(trait: keyof import('../api/types').AgentPersonality, value: number): string {
  const low = value < 0.33;
  const high = value > 0.66;
  const mid = !low && !high;
  switch (trait) {
    case 'curiosity': return high ? 'Highly curious' : low ? 'Uninterested in novelty' : 'Moderately curious';
    case 'aggression': return high ? 'Quick to anger' : low ? 'Avoids conflict' : 'Even-tempered';
    case 'empathy': return high ? 'Deeply empathetic' : low ? 'Self-focused' : 'Considerate';
    case 'sociability': return high ? 'Very social' : low ? 'Prefers solitude' : 'Sociable';
    case 'ambition': return high ? 'Ambitious' : low ? 'Content with little' : 'Moderately driven';
    case 'riskTolerance': return high ? 'Risk-taker' : low ? 'Cautious' : 'Balanced';
    case 'discipline': return high ? 'Highly disciplined' : low ? 'Unpredictable' : 'Disciplined';
    case 'generosity': return high ? 'Giving' : low ? 'Hoarding' : 'Fair-minded';
    default: return String(mid);
  }
}

export function interpretNeed(need: string, value: number): string {
  switch (need) {
    case 'hunger': return value >= 0.85 ? 'Starving' : value >= 0.5 ? 'Hungry' : value <= 0.15 ? 'Full' : 'Peckish';
    case 'energy': return value <= 0.1 ? 'Exhausted' : value <= 0.4 ? 'Tired' : value >= 0.9 ? 'Rested' : 'Adequate';
    case 'health': return value <= 0.2 ? 'Critically ill' : value <= 0.6 ? 'Unwell' : 'Healthy';
    case 'happiness': return value <= 0.2 ? 'Miserable' : value <= 0.5 ? 'Unhappy' : value >= 0.8 ? 'Joyful' : 'Content';
    case 'safety': return value <= 0.3 ? 'In danger' : 'Safe';
    case 'socialNeed': return value >= 0.8 ? 'Lonely' : value >= 0.5 ? 'Seeking company' : 'Socially fulfilled';
    default: return String(value.toFixed(2));
  }
}

export function interpretRelationship(rel: {
  trust: number;
  affection: number;
  anger: number;
  targetName: string | null;
}): string {
  const name = rel.targetName ?? 'them';
  if (rel.anger > 0.7) return `Hostile toward ${name}`;
  if (rel.affection >= 0.8 && rel.trust >= 0.8) return `Strong friendship with ${name}`;
  if (rel.affection >= 0.6) return `Close with ${name}`;
  if (rel.trust >= 0.7) return `Trusts ${name}`;
  if (rel.affection >= 0.5) return `Warm toward ${name}`;
  return `Distant from ${name}`;
}
