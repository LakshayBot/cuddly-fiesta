import { memo } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '../api/client';
import { useSimStore } from '../store/simStore';
import { STATUS_COLORS, STATUS_NAMES } from '../api/types';
import { formatSimDate, worldAgeDays } from '../lib/format';

const SPEEDS = [1, 5, 10, 50, 100, 200];

export const TopBar = memo(function TopBar({
  population,
  onExit,
}: {
  population: number;
  onExit: () => void;
}) {
  const world = useSimStore((s) => s.world);
  const queryClient = useQueryClient();
  const debugMode = useSimStore((s) => s.debugMode);
  const setDebugMode = useSimStore((s) => s.setDebugMode);
  const followAgent = useSimStore((s) => s.followAgent);

  const mutate = useMutation({
    mutationFn: (fn: () => Promise<unknown>) => fn(),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['world'] }),
  });

  const followRandom = () => {
    const map = useSimStore.getState().agentMap;
    const alive = [...map.values()].filter((a) => a.alive);
    if (alive.length === 0) return;
    const pick = alive[Math.floor(Math.random() * alive.length)];
    followAgent(pick.id);
  };

  if (!world) return null;

  const running = world.status === 2;
  const age = worldAgeDays(world.createdAt, world.currentSimulationTime);

  return (
    <header className="flex items-center gap-6 border-b border-zinc-800 bg-zinc-950/90 px-4 py-2 h-14">
      <div className="flex items-center gap-2">
        <button
          onClick={onExit}
          className="text-zinc-500 hover:text-zinc-200 text-sm font-mono"
          title="Back to worlds"
        >
          ◀
        </button>
        <h1 className="text-sm font-semibold tracking-wide text-zinc-100">{world.name}</h1>
        <span
          className="inline-flex items-center gap-1.5 text-xs text-zinc-400"
          title={STATUS_NAMES[world.status] ?? 'Unknown'}
        >
          <span
            className="h-2 w-2 rounded-full animate-pulse"
            style={{ backgroundColor: STATUS_COLORS[world.status] ?? '#a1a1aa' }}
          />
          {STATUS_NAMES[world.status] ?? 'Unknown'}
        </span>
      </div>

      <div className="flex items-center gap-4 text-xs font-mono text-zinc-400">
        <Stat label="Age" value={age} />
        <Stat label="Sim time" value={formatSimDate(world.currentSimulationTime)} />
        <Stat label="Tick" value={String(world.tickNumber)} />
        <Stat label="Population" value={String(population)} />
      </div>

      <div className="flex-1" />

      <div className="flex items-center gap-1">
        <button
          onClick={() => mutate.mutate(() => api.startWorld(world.id))}
          disabled={running || mutate.isPending}
          className="px-3 py-1.5 text-xs font-medium rounded-md bg-emerald-600 text-emerald-50 hover:bg-emerald-500 disabled:opacity-40"
        >
          Start
        </button>
        <button
          onClick={() => mutate.mutate(() => api.pauseWorld(world.id))}
          disabled={!running || mutate.isPending}
          className="px-3 py-1.5 text-xs font-medium rounded-md bg-amber-600 text-amber-50 hover:bg-amber-500 disabled:opacity-40"
        >
          Pause
        </button>
        <button
          onClick={followRandom}
          className="px-3 py-1.5 text-xs font-medium rounded-md border border-pink-500/50 text-pink-300 hover:bg-pink-500/10"
          title="Follow a random living agent"
        >
          👁 Follow random
        </button>
        <button
          onClick={() => setDebugMode(!debugMode)}
          className={`px-3 py-1.5 text-xs font-medium rounded-md border transition-colors ${
            debugMode
              ? 'border-fuchsia-500 bg-fuchsia-500/15 text-fuchsia-300'
              : 'border-zinc-700 text-zinc-400 hover:text-zinc-200'
          }`}
          title="Toggle developer debug values"
        >
          Debug
        </button>
      </div>

      <div className="flex items-center gap-1 rounded-md border border-zinc-800 p-0.5">
        {SPEEDS.map((speed) => (
          <button
            key={speed}
            onClick={() => mutate.mutate(() => api.setSpeed(world.id, speed))}
            className={`px-2 py-1 text-[11px] font-mono rounded transition-colors ${
              world.simulationSpeed === speed
                ? 'bg-emerald-600/80 text-emerald-50'
                : 'text-zinc-400 hover:text-zinc-100 hover:bg-zinc-800'
            }`}
          >
            {speed}×
          </button>
        ))}
      </div>
    </header>
  );
});

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-baseline gap-1.5">
      <span className="text-[10px] uppercase tracking-wider text-zinc-500">{label}</span>
      <span className="text-zinc-200">{value}</span>
    </div>
  );
}
