import { useState } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { WorldLauncher } from './components/WorldLauncher';
import { SimDashboard } from './components/SimDashboard';
import { useSimStore } from './store/simStore';

export default function App() {
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            retry: 1,
            refetchOnWindowFocus: false,
          },
        },
      }),
  );

  const activeWorldId = useSimStore((s) => s.world?.id) ?? null;
  const [launcherWorld, setLauncherWorld] = useState<string | null>(null);
  const worldId = activeWorldId ?? launcherWorld;

  return (
    <QueryClientProvider client={queryClient}>
      {worldId ? (
        <SimDashboard
          worldId={worldId}
          onExit={() => {
            useSimStore.getState().reset();
            setLauncherWorld(null);
          }}
        />
      ) : (
        <WorldLauncher onEnterWorld={(id) => setLauncherWorld(id)} />
      )}
    </QueryClientProvider>
  );
}
