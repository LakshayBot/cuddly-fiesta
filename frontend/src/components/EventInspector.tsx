import { memo, useEffect } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api } from '../api/client';
import { useSimStore } from '../store/simStore';
import { IMPORTANCE_COLORS, IMPORTANCE_NAMES } from '../api/types';
import { formatSimDate } from '../lib/format';

export const EventInspector = memo(function EventInspector({ eventId }: { eventId: string }) {
  const selectEvent = useSimStore((s) => s.selectEvent);
  const selectAgent = useSimStore((s) => s.selectAgent);

  const detail = useQuery({
    queryKey: ['event-detail', eventId],
    queryFn: () => api.getEventDetail(eventId),
    staleTime: 30_000,
  });

  useEffect(() => {
    if (eventId) void detail.refetch();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [eventId]);

  const evt = detail.data;

  return (
    <aside className="w-80 shrink-0 overflow-y-auto border-l border-zinc-800 bg-zinc-950/80">
      {!evt ? (
        <div className="p-4 text-sm text-zinc-500">Loading event…</div>
      ) : (
        <div className="p-4 space-y-4">
          <div className="flex items-start justify-between">
            <div>
              <div className="flex items-center gap-2">
                <h2 className="text-sm font-semibold text-zinc-100">Event</h2>
                <span
                  className="rounded px-1.5 py-0.5 text-[9px] font-medium uppercase"
                  style={{
                    color: IMPORTANCE_COLORS[evt.importance] ?? '#94a3b8',
                    border: `1px solid ${IMPORTANCE_COLORS[evt.importance] ?? '#94a3b8'}44`,
                    backgroundColor: `${IMPORTANCE_COLORS[evt.importance] ?? '#94a3b8'}18`,
                  }}
                >
                  {IMPORTANCE_NAMES[evt.importance] ?? 'Normal'}
                </span>
              </div>
              <p className="mt-1 font-mono text-[11px] text-zinc-500">
                #{evt.tick} · {formatSimDate(evt.simulationTime)} · {evt.eventType}
              </p>
            </div>
            <button onClick={() => selectEvent(null)} className="text-zinc-500 hover:text-zinc-200">
              ✕
            </button>
          </div>

          <Section title="What happened">
            <p className="text-xs text-zinc-300">
              {evt.actorName ? (
                <button onClick={() => evt.actorAgentId && selectAgent(evt.actorAgentId)} className="text-emerald-400 hover:underline">
                  {evt.actorName}
                </button>
              ) : null}{' '}
              {evt.eventType.replace('Agent', 'agent ').toLowerCase()}
              {evt.targetName ? (
                <>
                  {' '}
                  <button onClick={() => evt.targetAgentId && selectAgent(evt.targetAgentId)} className="text-emerald-400 hover:underline">
                    {evt.targetName}
                  </button>
                </>
              ) : null}
            </p>
            <p className="mt-1 text-[10px] font-mono text-zinc-600">importance score {evt.importanceScore.toFixed(1)}</p>
          </Section>

          <Section title="Why did this happen?">
            {evt.causes.length === 0 ? (
              <p className="text-xs text-zinc-600">No causes recorded for this event.</p>
            ) : (
              <ul className="space-y-1.5">
                {evt.causes.map((c) => (
                  <li key={c.id} className="text-[11px] leading-snug">
                    <span className={`font-medium ${CAUSE_COLORS[c.causeType] ?? 'text-zinc-400'}`}>
                      {c.causeType} · {c.name}
                      {c.value !== null ? ` ${c.value.toFixed(2)}` : ''}
                    </span>
                    <p className="text-zinc-500">{c.description}</p>
                  </li>
                ))}
              </ul>
            )}
          </Section>

          <Section title="What happened after?">
            <div className="space-y-2">
              {evt.directConsequences.length === 0 && evt.indirectConsequences.length === 0 && (
                <p className="text-xs text-zinc-600">No consequences recorded.</p>
              )}
              {evt.directConsequences.map((c) => (
                <div key={c.id} className="text-[11px]">
                  <span className="font-medium text-emerald-400">Direct · {c.consequenceType}</span>
                  <p className="text-zinc-400">{c.description}</p>
                </div>
              ))}
              {evt.indirectConsequences.map((c) => (
                <div key={c.id} className="text-[11px]">
                  <span className="font-medium text-sky-400">Indirect · {c.consequenceType}</span>
                  <p className="text-zinc-400">{c.description}</p>
                </div>
              ))}
            </div>
          </Section>
        </div>
      )}
    </aside>
  );
});

const CAUSE_COLORS: Record<string, string> = {
  Decision: 'text-fuchsia-400',
  State: 'text-amber-300',
  Event: 'text-sky-400',
  Resource: 'text-emerald-400',
  Relationship: 'text-pink-400',
};

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div>
      <h3 className="mb-1.5 text-[10px] font-semibold uppercase tracking-widest text-zinc-500">{title}</h3>
      {children}
    </div>
  );
}
