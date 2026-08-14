import { memo, useEffect, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api } from '../api/client';
import { useSimStore } from '../store/simStore';

export const ObserverOverlay = memo(function ObserverOverlay() {
  const followedAgentId = useSimStore((s) => s.followedAgentId);
  const followAgent = useSimStore((s) => s.followAgent);
  const selectAgent = useSimStore((s) => s.selectAgent);
  const agentMap = useSimStore((s) => s.agentMap);
  const [showLife, setShowLife] = useState(false);

  const followed = followedAgentId ? agentMap.get(followedAgentId) : undefined;
  const dead = followed ? !followed.alive : false;

  useEffect(() => {
    if (followedAgentId) setShowLife(false);
  }, [followedAgentId]);

  const life = useQuery({
    queryKey: ['agent-life', followedAgentId],
    queryFn: () => api.getAgentLife(followedAgentId!),
    enabled: Boolean(followedAgentId && (dead || showLife)),
  });

  if (!followedAgentId || !followed || !dead) return null;

  const agent = life.data;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 backdrop-blur-sm">
      <div className="w-[520px] max-h-[80vh] overflow-y-auto rounded-xl border border-rose-500/40 bg-zinc-950 p-6 shadow-2xl">
        <div className="mb-1 text-[10px] uppercase tracking-widest text-rose-400">Observer mode</div>
        <h2 className="text-xl font-semibold text-zinc-100">
          {followed.name} has died.
        </h2>
        <p className="mt-1 text-sm text-zinc-400">
          {followed.deathCause ?? 'Unknown cause'} at age {Math.round(followed.ageYears)}. Their story has ended.
        </p>

        {showLife && agent && (
          <div className="mt-4 space-y-3 border-t border-zinc-800 pt-4">
            <h3 className="text-xs font-semibold uppercase tracking-widest text-zinc-500">Life summary</h3>
            {agent.milestones.length === 0 ? (
              <p className="text-xs text-zinc-600">No significant life events recorded.</p>
            ) : (
              <ul className="space-y-2">
                {agent.milestones.map((m, i) => (
                  <li key={i} className="flex gap-2 text-xs">
                    <span className="shrink-0 font-mono text-zinc-600">
                      {m.tick > 0 ? `#${m.tick}` : ''}
                    </span>
                    <span className="text-zinc-300">{m.summary}</span>
                  </li>
                ))}
              </ul>
            )}
          </div>
        )}

        <div className="mt-5 flex gap-2">
          <button
            onClick={() => setShowLife((v) => !v)}
            className="rounded-md border border-zinc-700 px-4 py-2 text-xs font-medium text-zinc-200 hover:border-zinc-500"
          >
            {showLife ? 'Hide life summary' : 'View life summary'}
          </button>
          <button
            onClick={() => followRandomAgent()}
            className="rounded-md bg-emerald-600 px-4 py-2 text-xs font-medium text-emerald-50 hover:bg-emerald-500"
          >
            Follow another agent
          </button>
          <button
            onClick={() => {
              followAgent(null);
              selectAgent(null);
            }}
            className="rounded-md border border-zinc-700 px-4 py-2 text-xs font-medium text-zinc-400 hover:border-zinc-500"
          >
            Stop observing
          </button>
        </div>
      </div>
    </div>
  );

  function followRandomAgent() {
    const map = useSimStore.getState().agentMap;
    const alive = [...map.values()].filter((a) => a.alive);
    if (alive.length === 0) return;
    const pick = alive[Math.floor(Math.random() * alive.length)];
    useSimStore.getState().followAgent(pick.id);
  }
});
