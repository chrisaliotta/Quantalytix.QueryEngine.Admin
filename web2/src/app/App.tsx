import { RouterProvider } from 'react-router'
import { ThemeProvider } from '@/components/theme-provider'
import { Toaster } from '@/components/ui/sonner'
import { router } from './router'

/** Root component: theme, routing and toasts. */
export function App() {
  return (
    <ThemeProvider attribute="class" defaultTheme="light" enableSystem={false} disableTransitionOnChange>
      <RouterProvider router={router} />
      <Toaster position="bottom-right" />
    </ThemeProvider>
  )
}
