import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../api/client';
import { STATUS_COLORS, STATUS_NAMES, type World } from '../api/types';

export function WorldLauncher({ onEnterWorld }: { onEnterWorld: (worldId: string) => void }) {
  const queryClient = useQueryClient();
  const [draftName, setDraftName] = useState('');
  const [population, setPopulation] = useState(50);

  const worlds = useQuery({
    queryKey: ['worlds'],
    queryFn: api.listWorlds,
    refetchInterval: 3000,
  });

  const createWorld = useMutation({
    mutationFn: () => api.createWorld(draftName.trim(), population),
    onSuccess: (world) => {
      queryClient.invalidateQueries({ queryKey: ['worlds'] });
      setDraftName('');
      onEnterWorld(world.id);
    },
  });

  return (
    <div className="min-h-screen bg-zinc-950 text-zinc-200">
      <div className="mx-auto max-w-3xl px-6 py-10">
        <header className="mb-8">
          <h1 className="text-2xl font-semibold tracking-tight text-zinc-100">World Engine</h1>
          <p className="text-sm text-zinc-500 mt-1">
            Persistent civilization simulation — pick or create a world
          </p>
        </header>

        <section className="mb-8 rounded-lg border border-zinc-800 bg-zinc-900/60 p-4">
          <h2 className="mb-3 text-sm font-medium text-zinc-300">Create a new world</h2>
          <form
            onSubmit={(e) => {
              e.preventDefault();
              if (draftName.trim().length === 0) return;
              createWorld.mutate();
            }}
            className="flex flex-wrap items-center gap-2"
          >
            <input
              type="text"
              value={draftName}
              onChange={(e) => setDraftName(e.target.value)}
              placeholder="World name…"
              maxLength={200}
              className="flex-1 min-w-40 rounded-md border border-zinc-700 bg-zinc-950 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500/40"
            />
            <label className="flex items-center gap-2 text-xs text-zinc-400">
              Population
              <input
                type="number"
                min={0}
                max={1000}
                value={population}
                onChange={(e) => setPopulation(Number(e.target.value))}
                className="w-20 rounded-md border border-zinc-700 bg-zinc-950 px-2 py-2 text-sm focus:outline-none"
              />
            </label>
            <button
              type="submit"
              disabled={createWorld.isPending || draftName.trim().length === 0}
              className="rounded-md bg-emerald-500 px-4 py-2 text-sm font-medium text-zinc-950 hover:bg-emerald-400 disabled:opacity-50"
            >
              {createWorld.isPending ? 'Creating…' : 'Create'}
            </button>
          </form>
          {createWorld.isError && (
            <p className="mt-2 text-sm text-rose-400">{(createWorld.error as Error).message}</p>
          )}
        </section>

        <section>
          <h2 className="mb-3 text-sm font-medium text-zinc-300">Worlds</h2>
          {worlds.isLoading && <p className="text-sm text-zinc-500">Loading…</p>}
          {worlds.data && worlds.data.length === 0 && (
            <p className="text-sm text-zinc-500">No worlds yet — create one above.</p>
          )}
          <ul className="divide-y divide-zinc-800/80 rounded-lg border border-zinc-800/80 bg-zinc-900/40">
            {worlds.data?.map((world) => (
              <WorldRow key={world.id} world={world} onEnter={onEnterWorld} />
            ))}
          </ul>
        </section>
      </div>
    </div>
  );
}

function WorldRow({ world, onEnter }: { world: World; onEnter: (id: string) => void }) {
  return (
    <li className="flex items-center justify-between px-4 py-3">
      <button
        onClick={() => onEnter(world.id)}
        className="text-left text-emerald-400 hover:underline text-sm font-medium"
      >
        {world.name}
      </button>
      <div className="flex items-center gap-3 text-xs font-mono text-zinc-500">
        <span
          className="inline-flex items-center gap-1.5"
          title={STATUS_NAMES[world.status] ?? 'Unknown'}
        >
          <span
            className="h-2 w-2 rounded-full"
            style={{ backgroundColor: STATUS_COLORS[world.status] ?? '#a1a1aa' }}
          />
          {STATUS_NAMES[world.status] ?? '?'}
        </span>
        <span>tick {world.tickNumber}</span>
        <span>{world.simulationSpeed}×</span>
      </div>
    </li>
  );
}
