import { useEffect } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../api/client';
import { useSimStore } from '../store/simStore';
import { useSimulationRealtime } from '../realtime/useSimulationRealtime';
import { TopBar } from './TopBar';
import { WorldCanvas } from './WorldCanvas';
import { AgentInspector } from './AgentInspector';
import { EventInspector } from './EventInspector';
import { EventTimeline } from './EventTimeline';
import { ObserverOverlay } from './ObserverOverlay';
import { WorldHistoryModal } from './WorldHistoryModal';

export function SimDashboard({ worldId, onExit }: { worldId: string; onExit: () => void }) {
  const queryClient = useQueryClient();
  const setWorld = useSimStore((s) => s.setWorld);
  const setAgents = useSimStore((s) => s.setAgents);
  const selectedAgentId = useSimStore((s) => s.selectedAgentId);
  const selectedEventId = useSimStore((s) => s.selectedEventId);
  const selectAgent = useSimStore((s) => s.selectAgent);
  const reset = useSimStore((s) => s.reset);

  useSimulationRealtime(worldId);

  const world = useQuery({
    queryKey: ['world', worldId],
    queryFn: () => api.getWorld(worldId),
    refetchInterval: 5000,
  });

  const agents = useQuery({
    queryKey: ['agents', worldId],
    queryFn: () => api.listAgents(worldId, true, 500),
    refetchInterval: 1500,
  });

  const locations = useQuery({
    queryKey: ['locations', worldId],
    queryFn: () => api.listLocations(worldId),
    refetchInterval: 3000,
  });

  const settlements = useQuery({
    queryKey: ['settlements', worldId],
    queryFn: () => api.listSettlements(worldId),
    refetchInterval: 5000,
  });

  useEffect(() => {
    if (world.data) setWorld(world.data);
  }, [world.data, setWorld]);

  useEffect(() => {
    if (agents.data) setAgents(agents.data);
  }, [agents.data, setAgents]);

  useEffect(() => {
    if (!worldId) return;
    const ticker = window.setInterval(() => {
      void queryClient.invalidateQueries({ queryKey: ['world', worldId] });
    }, 10_000);
    return () => window.clearInterval(ticker);
  }, [worldId, queryClient]);

  useEffect(() => () => reset(), [reset]);

  return (
    <div className="flex h-screen flex-col bg-zinc-950 text-zinc-200">
      <TopBar population={agents.data?.length ?? 0} onExit={onExit} />
      <div className="flex min-h-0 flex-1">
        <WorldCanvas
          locations={locations.data ?? []}
          agents={agents.data ?? []}
          settlements={settlements.data ?? []}
          onSelectAgent={selectAgent}
        />
        {selectedAgentId && <AgentInspector agentId={selectedAgentId} />}
        {selectedEventId && <EventInspector eventId={selectedEventId} />}
      </div>
      <EventTimeline worldId={worldId} />
      <ObserverOverlay />
      <WorldHistoryModal worldId={worldId} />
    </div>
  );
}
