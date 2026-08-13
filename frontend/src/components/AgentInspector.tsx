import { memo, useEffect } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api } from '../api/client';
import { useSimStore } from '../store/simStore';
import type { AgentNeeds, AgentPersonality } from '../api/types';

const NEED_LABELS: { key: keyof AgentNeeds; label: string; invert?: boolean }[] = [
  { key: 'hunger', label: 'Hunger' },
  { key: 'energy', label: 'Energy', invert: true },
  { key: 'health', label: 'Health', invert: true },
  { key: 'happiness', label: 'Happiness', invert: true },
  { key: 'safety', label: 'Safety', invert: true },
  { key: 'socialNeed', label: 'Social need' },
];

const PERSONALITY_LABELS: { key: keyof AgentPersonality; label: string }[] = [
  { key: 'curiosity', label: 'Curiosity' },
  { key: 'aggression', label: 'Aggression' },
  { key: 'empathy', label: 'Empathy' },
  { key: 'sociability', label: 'Sociability' },
  { key: 'ambition', label: 'Ambition' },
  { key: 'riskTolerance', label: 'Risk tolerance' },
  { key: 'discipline', label: 'Discipline' },
  { key: 'generosity', label: 'Generosity' },
];

export const AgentInspector = memo(function AgentInspector({ agentId }: { agentId: string }) {
  const followAgent = useSimStore((s) => s.followAgent);
  const followedAgentId = useSimStore((s) => s.followedAgentId);
  const isFollowing = followedAgentId === agentId;

  const agent = useQuery({
    queryKey: ['agent', agentId],
    queryFn: () => api.getAgent(agentId),
    refetchInterval: isFollowing ? 1500 : 10_000,
  });

  const relationships = useQuery({
    queryKey: ['relationships', agentId],
    queryFn: () => api.getAgentRelationships(agentId, 'outgoing'),
    refetchInterval: isFollowing ? 5000 : 30_000,
  });

  const memories = useQuery({
    queryKey: ['memories', agentId],
    queryFn: () => api.getAgentMemories(agentId, 15),
    refetchInterval: isFollowing ? 5000 : 30_000,
  });

  const decisions = useQuery({
    queryKey: ['decisions', agentId],
    queryFn: () => api.getAgentDecisions(agentId, 3),
    refetchInterval: isFollowing ? 2000 : 10_000,
  });

  useEffect(() => {
    if (agentId) {
      void agent.refetch();
      void relationships.refetch();
      void memories.refetch();
      void decisions.refetch();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [agentId]);

  const a = agent.data;

  return (
    <aside className="w-80 shrink-0 overflow-y-auto border-l border-zinc-800 bg-zinc-950/80">
      {!a ? (
        <div className="p-4 text-sm text-zinc-500">Loading agent…</div>
      ) : (
        <div className="p-4 space-y-4">
          <div className="flex items-start justify-between">
            <div>
              <h2 className="text-base font-semibold text-zinc-100">{a.name}</h2>
              <p className="text-xs text-zinc-500">
                {a.occupation} · {Math.round(a.ageYears)} yrs · {a.location}
              </p>
              <p className="text-xs text-zinc-500">💰 {a.money.toFixed(2)}</p>
            </div>
            <button
              onClick={() => followAgent(isFollowing ? null : a.id)}
              className={`px-2.5 py-1.5 text-xs rounded-md border transition-colors ${
                isFollowing
                  ? 'border-pink-500 bg-pink-500/15 text-pink-300'
                  : 'border-zinc-700 text-zinc-300 hover:border-pink-500/60 hover:text-pink-300'
              }`}
            >
              {isFollowing ? 'Unfollow' : 'Follow'}
            </button>
          </div>

          {!a.alive && (
            <div className="rounded-md border border-rose-500/40 bg-rose-500/10 px-3 py-2 text-xs text-rose-300">
              Died: {a.deathCause ?? 'unknown'}
            </div>
          )}

          <Section title="Current action">
            {decisions.data?.[0] ? (
              <div className="space-y-1">
                <div className="flex items-center justify-between">
                  <span className="text-sm text-emerald-300">{decisions.data[0].selectedActionType}</span>
                  <span className="text-[10px] font-mono text-zinc-500">
                    score {decisions.data[0].selectedScore.toFixed(1)}
                  </span>
                </div>
                {decisions.data[0].reasoning && (
                  <p className="text-[11px] text-zinc-500">{decisions.data[0].reasoning}</p>
                )}
                {decisions.data[0].decisionSource.startsWith('LLM') && (
                  <p className="text-[10px] text-fuchsia-400">
                    LLM · {decisions.data[0].modelName} {decisions.data[0].fallbackUsed ? '(fallback)' : ''}
                  </p>
                )}
              </div>
            ) : (
              <p className="text-xs text-zinc-600">No decision recorded yet</p>
            )}
          </Section>

          <Section title="Needs">
            <div className="space-y-1.5">
              {NEED_LABELS.map(({ key, label, invert }) => (
                <BarRow key={key} label={label} value={a.needs[key]} invert={invert} />
              ))}
            </div>
          </Section>

          <Section title="Personality">
            <div className="space-y-1.5">
              {PERSONALITY_LABELS.map(({ key, label }) => (
                <BarRow key={key} label={label} value={a.personality[key]} />
              ))}
            </div>
          </Section>

          <Section title="Relationships">
            {relationships.data && relationships.data.length > 0 ? (
              <div className="space-y-2">
                {relationships.data
                  .sort((x, y) => y.affection + y.trust - (x.affection + x.trust))
                  .slice(0, 6)
                  .map((r) => (
                    <div key={`${r.sourceAgentId}-${r.targetAgentId}`} className="text-[11px]">
                      <div className="flex items-center justify-between text-zinc-300">
                        <span>{r.targetName ?? '#' + r.targetAgentId.slice(0, 4)}</span>
                        <span className="text-zinc-500">
                          ♥{r.affection.toFixed(2)} · ✦{r.trust.toFixed(2)}
                          {r.anger > 0.5 && <span className="text-rose-400"> · ⚡{r.anger.toFixed(2)}</span>}
                        </span>
                      </div>
                    </div>
                  ))}
              </div>
            ) : (
              <p className="text-xs text-zinc-600">No relationships yet</p>
            )}
          </Section>

          <Section title="Memories">
            {memories.data && memories.data.length > 0 ? (
              <ul className="space-y-1.5">
                {memories.data.slice(0, 6).map((m) => (
                  <li key={m.id} className="text-[11px] leading-snug text-zinc-400">
                    <span className="text-zinc-500">[{m.type}]</span> {m.summary}
                  </li>
                ))}
              </ul>
            ) : (
              <p className="text-xs text-zinc-600">No memories yet</p>
            )}
          </Section>
        </div>
      )}
    </aside>
  );
});

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div>
      <h3 className="mb-1.5 text-[10px] font-semibold uppercase tracking-widest text-zinc-500">
        {title}
      </h3>
      {children}
    </div>
  );
}

function BarRow({
  label,
  value,
  invert,
}: {
  label: string;
  value: number;
  invert?: boolean;
}) {
  const danger = invert ? value <= 0.25 : value >= 0.75;
  const pct = Math.max(0, Math.min(100, value * 100));
  return (
    <div className="flex items-center gap-2">
      <span className="w-20 text-[10px] text-zinc-500">{label}</span>
      <div className="h-1.5 flex-1 overflow-hidden rounded-full bg-zinc-800">
        <div
          className={`h-full rounded-full ${danger ? 'bg-rose-500' : 'bg-emerald-500'}`}
          style={{ width: `${pct}%` }}
        />
      </div>
      <span className="w-8 text-right font-mono text-[10px] text-zinc-400">{value.toFixed(2)}</span>
    </div>
  );
}
