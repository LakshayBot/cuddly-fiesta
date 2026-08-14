import { memo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api } from '../api/client';
import { IMPORTANCE_COLORS, IMPORTANCE_NAMES } from '../api/types';
import { formatSimDate } from '../lib/format';

export const WorldHistoryModal = memo(function WorldHistoryModal({ worldId }: { worldId: string }) {
  const [open, setOpen] = useState(false);
  const [filter, setFilter] = useState(0);

  const history = useQuery({
    queryKey: ['world-history', worldId, filter],
    queryFn: () => api.getWorldHistory(worldId, { limit: 100, minImportance: filter }),
    enabled: open,
  });

  return (
    <>
      <button
        onClick={() => setOpen(true)}
        className="absolute bottom-1 right-1 z-40 rounded border border-zinc-700 bg-zinc-900/90 px-3 py-1 text-[10px] text-zinc-300 hover:border-zinc-500"
        style={{ position: 'fixed', bottom: 216, right: 12 }}
        title="World history"
      >
        📜 World History
      </button>

      {open && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 backdrop-blur-sm">
          <div className="w-[640px] max-h-[80vh] overflow-y-auto rounded-xl border border-zinc-700 bg-zinc-950 p-6">
            <div className="flex items-center justify-between">
              <h2 className="text-base font-semibold text-zinc-100">World History</h2>
              <button onClick={() => setOpen(false)} className="text-zinc-500 hover:text-zinc-200">✕</button>
            </div>

            <div className="mt-3 flex items-center gap-1">
              {[0, 2, 3, 4].map((level) => (
                <button
                  key={level}
                  onClick={() => setFilter(level)}
                  className={`rounded px-2 py-0.5 text-[10px] ${
                    filter === level ? 'bg-emerald-600/70 text-emerald-50' : 'text-zinc-500 hover:text-zinc-200'
                  }`}
                >
                  {level === 0 ? 'All' : `${IMPORTANCE_NAMES[level]}+`}
                </button>
              ))}
            </div>

            <div className="mt-4 space-y-2">
              {history.isLoading && <p className="text-xs text-zinc-600">Loading…</p>}
              {history.data && history.data.length === 0 && (
                <p className="text-xs text-zinc-600">No significant history yet — the world is young.</p>
              )}
              {history.data?.map((h) => (
                <div key={h.id} className="rounded-lg border border-zinc-800 bg-zinc-900/50 p-3">
                  <div className="flex items-center justify-between">
                    <span
                      className="rounded px-1.5 py-0.5 text-[9px] font-medium uppercase"
                      style={{ color: IMPORTANCE_COLORS[h.importance] ?? '#94a3b8', border: `1px solid ${IMPORTANCE_COLORS[h.importance] ?? '#94a3b8'}44` }}
                    >
                      {IMPORTANCE_NAMES[h.importance] ?? 'Normal'} · {h.entryType}
                    </span>
                    <span className="font-mono text-[9px] text-zinc-600">
                      {formatSimDate(h.simulationTime)}
                    </span>
                  </div>
                  <p className="mt-1.5 text-xs text-zinc-300">{h.summary}</p>
                </div>
              ))}
            </div>
          </div>
        </div>
      )}
    </>
  );
});
