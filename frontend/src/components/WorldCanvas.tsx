import { memo, useMemo } from 'react';
import type { Agent, Location, Settlement } from '../api/types';
import { useSimStore } from '../store/simStore';

const W = 900;
const H = 520;

const LOCATION_POS: Record<string, { x: number; y: number }> = {
  Village: { x: 450, y: 330 },
  Farm: { x: 690, y: 160 },
  Forest: { x: 200, y: 130 },
  River: { x: 450, y: 460 },
};

const OCCUPATION_COLORS: Record<string, string> = {
  Farmer: '#4ade80',
  Woodcutter: '#d6a15c',
  Worker: '#60a5fa',
  Unemployed: '#94a3b8',
};

function hashToOffset(id: string): { dx: number; dy: number } {
  let h = 0;
  for (let i = 0; i < id.length; i++) h = (h * 31 + id.charCodeAt(i)) | 0;
  const angle = (h % 360) * (Math.PI / 180);
  const radius = 8 + (Math.abs(h >> 3) % 26);
  return { dx: Math.cos(angle) * radius, dy: Math.sin(angle) * radius * 0.75 };
}

export const WorldCanvas = memo(function WorldCanvas({
  locations,
  agents,
  settlements,
  onSelectAgent,
}: {
  locations: Location[];
  agents: Agent[];
  settlements: Settlement[];
  onSelectAgent: (id: string) => void;
}) {
  const selectedAgentId = useSimStore((s) => s.selectedAgentId);
  const followedAgentId = useSimStore((s) => s.followedAgentId);
  const agentMap = useSimStore((s) => s.agentMap);

  const settlementByLocation = useMemo(() => {
    const m = new Map<string, Settlement>();
    for (const s of settlements) m.set(s.centerLocationName, s);
    return m;
  }, [settlements]);

  const agentsByLocation = useMemo(() => {
    const m = new Map<string, Agent[]>();
    for (const a of agents) {
      if (!a.alive) continue;
      const list = m.get(a.location) ?? [];
      list.push(a);
      m.set(a.location, list);
    }
    return m;
  }, [agents]);

  const followed = followedAgentId ? agentMap.get(followedAgentId) : undefined;

  return (
    <div className="relative flex-1 overflow-hidden bg-[radial-gradient(circle_at_50%_40%,#101418_0%,#080a0d_70%)]">
      <svg
        viewBox={`0 0 ${W} ${H}`}
        className="h-full w-full"
        preserveAspectRatio="xMidYMid meet"
      >
        <defs>
          <radialGradient id="locGlow" cx="50%" cy="50%" r="50%">
            <stop offset="0%" stopColor="#22d3ee" stopOpacity="0.16" />
            <stop offset="100%" stopColor="#22d3ee" stopOpacity="0" />
          </radialGradient>
        </defs>

        {locations.map((loc) => {
          const pos = LOCATION_POS[loc.type] ?? { x: 450, y: 300 };
          const settlement = settlementByLocation.get(loc.name);
          const residents = agentsByLocation.get(loc.name)?.length ?? 0;
          return (
            <g key={loc.id}>
              {settlement && (
                <circle
                  cx={pos.x}
                  cy={pos.y}
                  r={56}
                  fill="none"
                  stroke="#fbbf24"
                  strokeOpacity="0.65"
                  strokeWidth="1.5"
                  strokeDasharray="4 3"
                />
              )}
              <circle cx={pos.x} cy={pos.y} r={34} fill="url(#locGlow)" />
              <circle
                cx={pos.x}
                cy={pos.y}
                r={10}
                fill="#1c2129"
                stroke={settlement ? '#fbbf24' : '#3f4a58'}
                strokeWidth={1.5}
              />
              <text x={pos.x} y={pos.y + 4} textAnchor="middle" fontSize="9" fill="#cbd5e1" fontWeight={600}>
                {loc.name}
              </text>
              {settlement && (
                <text x={pos.x} y={pos.y + 34} textAnchor="middle" fontSize="9" fill="#fbbf24" fontWeight={600}>
                  {settlement.name} · {settlement.population}
                </text>
              )}
              <text x={pos.x} y={pos.y - 16} textAnchor="middle" fontSize="8" fill="#64748b">
                {residents} here
              </text>
            </g>
          );
        })}

        {agents.map((agent) => {
          const pos = LOCATION_POS[agent.location] ?? { x: 450, y: 300 };
          const { dx, dy } = hashToOffset(agent.id);
          const cx = pos.x + dx;
          const cy = pos.y + dy;
          const isSelected = selectedAgentId === agent.id;
          const isFollowed = followedAgentId === agent.id;
          const color = OCCUPATION_COLORS[agent.occupation] ?? '#94a3b8';
          return (
            <g
              key={agent.id}
              transform={`translate(${cx}, ${cy})`}
              className="cursor-pointer"
              onClick={(e) => {
                e.stopPropagation();
                onSelectAgent(agent.id);
              }}
            >
              {isFollowed && (
                <circle r={9} fill="none" stroke="#f472b6" strokeWidth="1.5" className="animate-ping" />
              )}
              <circle r={4.5} fill={color} opacity={isSelected || isFollowed ? 1 : 0.75} />
              {isSelected && <circle r={7.5} fill="none" stroke="#ffffff" strokeWidth="1" />}
              {isSelected || isFollowed ? (
                <text y={-9} textAnchor="middle" fontSize="8" fill="#e2e8f0" fontWeight={600}>
                  {agent.name.split(' ')[0]}
                </text>
              ) : null}
            </g>
          );
        })}
      </svg>

      {followed && (
        <div className="absolute bottom-3 left-3 rounded-md border border-pink-500/40 bg-zinc-950/80 px-3 py-1.5 text-xs">
          <span className="text-pink-400 font-medium">Following:</span>{' '}
          <span className="text-zinc-200">{followed.name}</span>{' '}
          <span className="text-zinc-500">({followed.occupation} @ {followed.location})</span>
        </div>
      )}

      <div className="absolute top-3 left-3 flex flex-col gap-1">
        {Object.entries(OCCUPATION_COLORS).map(([occ, color]) => (
          <div key={occ} className="flex items-center gap-1.5 text-[10px] text-zinc-400">
            <span className="h-2 w-2 rounded-full" style={{ backgroundColor: color }} />
            {occ}
          </div>
        ))}
      </div>
    </div>
  );
});
