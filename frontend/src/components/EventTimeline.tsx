import { memo, useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api } from '../api/client';
import { useSimStore } from '../store/simStore';
import {
  CATEGORY_LABELS,
  formatSimDate,
  toEventView,
  type EventCategory,
} from '../lib/format';
import type { SimulationEvent } from '../api/types';

const FILTERS: (EventCategory | 'all')[] = [
  'all',
  'social',
  'economy',
  'conflict',
  'death',
  'birth',
  'settlement',
  'movement',
];

export const EventTimeline = memo(function EventTimeline({ worldId }: { worldId: string }) {
  const liveEvents = useSimStore((s) => s.liveEvents);
  const eventFilter = useSimStore((s) => s.eventFilter);
  const setEventFilter = useSimStore((s) => s.setEventFilter);
  const agentMap = useSimStore((s) => s.agentMap);
  const selectAgent = useSimStore((s) => s.selectAgent);
  const selectEvent = useSimStore((s) => s.selectEvent);

  const [showHistory, setShowHistory] = useState(false);
  const [historyBefore, setHistoryBefore] = useState<number | undefined>(undefined);

  const filtered = useMemo(
    () =>
      eventFilter === 'all'
        ? liveEvents
        : liveEvents.filter((e) => e.category === eventFilter),
    [liveEvents, eventFilter],
  );

  const history = useQuery({
    queryKey: ['events-history', worldId, historyBefore],
    queryFn: async () => {
      const events = await api.listEvents(worldId, {
        limit: 100,
        beforeTick: historyBefore,
      });
      return events;
    },
    enabled: showHistory,
  });

  const loadEarlier = () => {
    if (!history.data || history.data.length === 0) return;
    const oldestTick = history.data[history.data.length - 1].tick;
    setHistoryBefore(oldestTick);
  };

  return (
    <div className="flex h-52 shrink-0 flex-col border-t border-zinc-800 bg-zinc-950/90">
      <div className="flex items-center gap-2 border-b border-zinc-800/70 px-3 py-1.5">
        <span className="text-[10px] font-semibold uppercase tracking-widest text-zinc-500">
          Events
        </span>
        <div className="flex items-center gap-1">
          {FILTERS.map((f) => (
            <button
              key={f}
              onClick={() => setEventFilter(f)}
              className={`rounded px-2 py-0.5 text-[10px] transition-colors ${
                eventFilter === f
                  ? 'bg-emerald-600/70 text-emerald-50'
                  : 'text-zinc-500 hover:text-zinc-200 hover:bg-zinc-800'
              }`}
            >
              {f === 'all' ? 'All' : CATEGORY_LABELS[f]}
            </button>
          ))}
        </div>
        <div className="flex-1" />
        <button
          onClick={() => setShowHistory((v) => !v)}
          className={`text-[10px] rounded px-2 py-0.5 ${
            showHistory ? 'bg-zinc-700 text-zinc-100' : 'text-zinc-500 hover:text-zinc-200'
          }`}
        >
          History
        </button>
      </div>

      <div className="flex-1 overflow-y-auto px-3 py-1.5">
        {showHistory ? (
          <div className="space-y-0.5">
            {history.data?.map((e) => (
              <EventRow key={e.id} evt={e} agentMap={agentMap} />
            ))}
            {history.isLoading && <p className="text-xs text-zinc-600">Loading history…</p>}
            {history.data && history.data.length > 0 && (
              <button
                onClick={loadEarlier}
                className="mt-1 w-full rounded border border-zinc-800 py-1 text-[10px] text-zinc-500 hover:text-zinc-200"
              >
                Load earlier
              </button>
            )}
          </div>
        ) : filtered.length === 0 ? (
          <p className="text-xs text-zinc-600 pt-2">Waiting for events…</p>
        ) : (
          <div className="space-y-0.5">
            {filtered.slice(0, 60).map((e) => (
              <div
                key={e.id}
                className="flex cursor-pointer items-baseline gap-2 text-[11px] rounded px-1 hover:bg-zinc-900"
                onClick={() => selectEvent(e.id)}
              >
                <span className="shrink-0 font-mono text-[9px] text-zinc-600">
                  {formatSimDate(e.simulationTime)}
                </span>
                <Dot category={e.category} />
                <span className="truncate text-zinc-300">{e.text}</span>
                {e.actorAgentId && (
                  <button
                    onClick={(ev) => {
                      ev.stopPropagation();
                      if (e.actorAgentId) selectAgent(e.actorAgentId);
                    }}
                    className="shrink-0 text-[9px] text-emerald-500/70 hover:text-emerald-300"
                  >
                    inspect
                  </button>
                )}
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
});

const CATEGORY_COLORS: Record<string, string> = {
  social: '#34d399',
  economy: '#60a5fa',
  death: '#f87171',
  birth: '#fbbf24',
  conflict: '#fb7185',
  settlement: '#fbbf24',
  movement: '#a78bfa',
  misc: '#94a3b8',
};

function Dot({ category }: { category: EventCategory }) {
  return (
    <span
      className="h-1.5 w-1.5 shrink-0 rounded-full"
      style={{ backgroundColor: CATEGORY_COLORS[category] ?? '#94a3b8' }}
    />
  );
}

function EventRow({ evt, agentMap }: { evt: SimulationEvent; agentMap: Map<string, import('../api/types').Agent> }) {
  const view = toEventView(evt, agentMap);
  return (
    <div className="flex items-baseline gap-2 text-[11px]">
      <span className="shrink-0 font-mono text-[9px] text-zinc-600">
        #{evt.tick} {formatSimDate(evt.simulationTime)}
      </span>
      <Dot category={view.category} />
      <span className="truncate text-zinc-300">{view.text}</span>
    </div>
  );
}
