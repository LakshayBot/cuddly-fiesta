import { useEffect, useRef } from 'react';
import * as signalR from '@microsoft/signalr';
import { api, HUB_URL } from '../api/client';
import { useSimStore } from '../store/simStore';
import { toEventView } from '../lib/format';
import type { SignalEvent, WorldStateUpdate } from '../api/types';

let connection: signalR.HubConnection | null = null;

export function getSimulationHub(): signalR.HubConnection {
  if (connection) return connection;

  connection = new signalR.HubConnectionBuilder()
    .withUrl(HUB_URL)
    .withAutomaticReconnect()
    .build();

  return connection;
}

const BATCH_MS = 250;

export function useSimulationRealtime(worldId: string) {
  const setWorld = useSimStore((s) => s.setWorld);
  const pushEvents = useSimStore((s) => s.pushEvents);
  const agentMap = useSimStore((s) => s.agentMap);
  const agentMapRef = useRef(agentMap);
  agentMapRef.current = agentMap;

  const pendingEvents = useRef<SignalEvent[]>([]);

  useEffect(() => {
    if (!worldId) return;
    const hub = getSimulationHub();
    const onWorldState = (update: WorldStateUpdate) => {
      if (update.worldId !== worldId) return;
      setWorld({
        id: update.worldId,
        name: useSimStore.getState().world?.name ?? '',
        randomSeed: useSimStore.getState().world?.randomSeed ?? 0,
        currentSimulationTime: update.simulationTime,
        simulationSpeed: update.speed,
        status: update.status,
        tickNumber: update.tickNumber,
        createdAt: useSimStore.getState().world?.createdAt ?? new Date().toISOString(),
        updatedAt: update.updatedAt,
      });
    };

    const onEvent = (evt: SignalEvent) => {
      if (evt.worldId !== worldId) return;
      pendingEvents.current.push(evt);
    };

    const flush = () => {
      if (pendingEvents.current.length === 0) return;
      const batch = pendingEvents.current.splice(0);
      const views = batch.map((e) => toEventView(e, agentMapRef.current));
      pushEvents(views);
    };

    const timer = window.setInterval(flush, BATCH_MS);

    const start = async () => {
      if (hub.state === signalR.HubConnectionState.Disconnected) {
        try {
          await hub.start();
        } catch {
          // retry via automatic reconnect
        }
      }
    };

    hub.on('WorldStateUpdated', onWorldState);
    hub.on('SimulationEventOccurred', onEvent);
    void start();

    return () => {
      window.clearInterval(timer);
      flush();
      hub.off('WorldStateUpdated', onWorldState);
      hub.off('SimulationEventOccurred', onEvent);
    };
  }, [worldId, setWorld, pushEvents]);

  // One-time backfill of missed events since the store last saw
  useEffect(() => {
    if (!worldId) return;
    const sinceTick = useSimStore.getState().eventsSinceTick;
    if (sinceTick > 0) return;
    void api.listEvents(worldId, { limit: 100 }).then((events) => {
      const views = events.map((e) => toEventView(e, agentMapRef.current));
      pushEvents(views);
    });
  }, [worldId, pushEvents]);
}
