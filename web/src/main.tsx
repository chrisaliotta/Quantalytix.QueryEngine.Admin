import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import '@fontsource/ibm-plex-sans/400.css'
import '@fontsource/ibm-plex-sans/500.css'
import '@fontsource/ibm-plex-sans/600.css'
import '@fontsource/ibm-plex-mono/400.css'
import '@fontsource/ibm-plex-mono/500.css'
import { apiMode, ensureDevelopmentToken } from '@/lib/api/session'
import { loadModels } from '@/lib/model/persistence'
import './index.css'

if (apiMode === 'http') {
  await ensureDevelopmentToken()
  await loadModels()
}

// Imported after loading, so the screens' stores start from what was loaded.
const { App } = await import('@/app/App')

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
