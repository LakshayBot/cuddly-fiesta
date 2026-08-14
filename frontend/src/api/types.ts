export type SimulationStatus = 'Uninitialized' | 'Paused' | 'Running' | 'Stopped';

export interface World {
  id: string;
  name: string;
  randomSeed: number;
  currentSimulationTime: string;
  simulationSpeed: number;
  status: number;
  tickNumber: number;
  createdAt: string;
  updatedAt: string;
}

export interface Health {
  status: string;
  version: string;
  timestamp: string;
}

export interface AgentNeeds {
  hunger: number;
  energy: number;
  health: number;
  happiness: number;
  safety: number;
  socialNeed: number;
}

export interface AgentPersonality {
  curiosity: number;
  aggression: number;
  empathy: number;
  sociability: number;
  ambition: number;
  riskTolerance: number;
  discipline: number;
  generosity: number;
}

export interface Agent {
  id: string;
  worldId: string;
  name: string;
  ageYears: number;
  birthSimulationTime: string;
  alive: boolean;
  deathSimulationTime: string | null;
  deathCause: string | null;
  location: string;
  occupation: string;
  money: number;
  needs: AgentNeeds;
  personality: AgentPersonality;
  createdAt: string;
  updatedAt: string;
}

export interface Relationship {
  sourceAgentId: string;
  targetAgentId: string;
  targetName: string | null;
  trust: number;
  affection: number;
  respect: number;
  fear: number;
  anger: number;
  familiarity: number;
  updatedAt: string;
}

export interface Memory {
  id: string;
  agentId: string;
  simulationEventId: string;
  type: string;
  importance: number;
  emotionalImpact: number;
  createdSimulationTime: string;
  otherAgentId: string | null;
  summary: string;
  createdAt: string;
}

export interface DecisionActionScore {
  id: string;
  type: string;
  score: number;
  reasoning: string | null;
}

export interface Decision {
  id: string;
  agentId: string;
  tick: number;
  simulationTime: string;
  decisionSource: string;
  selectedActionId: string;
  selectedActionType: string;
  selectedScore: number;
  availableActions: DecisionActionScore[];
  reasoning: string | null;
  decidedAt: string;
  modelName: string | null;
  promptVersion: string | null;
  latencyMs: number | null;
  fallbackUsed: boolean;
}

export interface Location {
  id: string;
  worldId: string;
  name: string;
  type: string;
  resources: Record<string, number>;
  updatedAt: string;
}

export interface SimulationEvent {
  id: string;
  tick: number;
  simulationTime: string;
  eventType: string;
  actorAgentId: string | null;
  targetAgentId: string | null;
  locationId: string | null;
  data: string;
  importance: number;
  importanceScore: number;
  createdAt: string;
}

export interface Settlement {
  id: string;
  worldId: string;
  name: string;
  centerLocationName: string;
  population: number;
  status: string;
  formationReason: string;
  firstPopulationAtTick: string;
  creationSimulationTime: string;
  updatedAt: string;
}

export interface GroupMember {
  agentId: string;
  name: string;
  role: string;
  joinedAt: string;
}

export interface Group {
  id: string;
  worldId: string;
  name: string;
  type: string;
  status: string;
  formationReason: string;
  formationSimulationTime: string;
  members: GroupMember[];
}

export interface WorldStateUpdate {
  worldId: string;
  tickNumber: number;
  simulationTime: string;
  speed: number;
  status: number;
  updatedAt: string;
}

export interface SignalEvent {
  id: string;
  worldId: string;
  tick: number;
  simulationTime: string;
  eventType: string;
  actorAgentId: string | null;
  targetAgentId: string | null;
  locationId: string | null;
  importance: number;
  data: Record<string, unknown>;
}

export const IMPORTANCE_NAMES: Record<number, string> = {
  0: 'Trivial',
  1: 'Normal',
  2: 'Significant',
  3: 'Major',
  4: 'Historical',
};

export const IMPORTANCE_COLORS: Record<number, string> = {
  0: '#3f4a58',
  1: '#64748b',
  2: '#f59e0b',
  3: '#f97316',
  4: '#ef4444',
};

export interface EventCause {
  id: string;
  causeType: string;
  causeEventId: string | null;
  decisionRecordId: string | null;
  name: string;
  value: number | null;
  description: string;
  createdTick: number;
}

export interface EventConsequence {
  id: string;
  kind: string;
  consequenceType: string;
  consequenceEventId: string | null;
  consequenceMemoryId: string | null;
  description: string;
  createdTick: number;
}

export interface EventDetail {
  id: string;
  tick: number;
  simulationTime: string;
  eventType: string;
  actorAgentId: string | null;
  actorName: string | null;
  targetAgentId: string | null;
  targetName: string | null;
  locationId: string | null;
  data: string;
  importance: number;
  importanceScore: number;
  causes: EventCause[];
  directConsequences: EventConsequence[];
  indirectConsequences: EventConsequence[];
}

export interface LifeMilestone {
  tick: number;
  simulationTime: string;
  type: string;
  importance: number;
  summary: string;
  eventId: string | null;
}

export interface AgentLife {
  agentId: string;
  name: string;
  alive: boolean;
  ageYears: number;
  occupation: string;
  location: string;
  currentAction: string | null;
  currentReasoning: string | null;
  milestones: LifeMilestone[];
}

export interface WorldHistoryEntry {
  id: string;
  tick: number;
  simulationTime: string;
  entryType: string;
  importance: number;
  factsJson: string;
  summary: string;
  relatedEventId: string | null;
}

export interface AutopsyFactor {
  tick: number;
  simulationTime: string;
  eventType: string;
  eventId: string | null;
  description: string;
}

export interface Autopsy {
  subject: string;
  summary: string;
  timeline: AutopsyFactor[];
}

export const STATUS_NAMES: Record<number, SimulationStatus> = {
  0: 'Uninitialized',
  1: 'Paused',
  2: 'Running',
  3: 'Stopped',
};

export const STATUS_COLORS: Record<number, string> = {
  0: '#a1a1aa',
  1: '#f59e0b',
  2: '#34d399',
  3: '#f87171',
};
