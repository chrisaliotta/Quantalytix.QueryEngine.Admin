import { QueryClientProvider } from '@tanstack/react-query';
import { RouterProvider } from 'react-router';
import { Toaster } from '@/components/ui/sonner';
import { TooltipProvider } from '@/components/ui/tooltip';
import { queryClient } from '@/api/queryClient';
import { SessionProvider } from '@/auth/SessionProvider';
import { ThemeProvider, useTheme } from '@/contexts/Theme';
import { router } from './router';

/** Root component: theme, server state, session, tooltips, routing and toasts. */
export function App() {
  return (
    <ThemeProvider theme="dark">
      <QueryClientProvider client={queryClient}>
        <SessionProvider>
          <TooltipProvider>
            <RouterProvider router={router} />
            <AppToaster />
          </TooltipProvider>
        </SessionProvider>
      </QueryClientProvider>
    </ThemeProvider>
  );
}

/** The shadcn Toaster reads its theme from next-themes, which this app does not use. */
function AppToaster() {
  const { theme } = useTheme();
  return <Toaster theme={theme === 'light' ? 'light' : 'dark'} richColors />;
}
