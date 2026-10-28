import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import '@fontsource/ibm-plex-sans/400.css'
import '@fontsource/ibm-plex-sans/500.css'
import '@fontsource/ibm-plex-sans/600.css'
import '@fontsource/ibm-plex-mono/400.css'
import '@fontsource/ibm-plex-mono/500.css'
import { apiMode, ensureDevelopmentToken } from '@/lib/api/session'
import { loadModels } from '@/lib/model/persistence'
import { loadReports } from '@/lib/report/persistence'
import './index.css'

if (apiMode === 'http') {
  await ensureDevelopmentToken()
  await loadModels()
  await loadReports()
}

// Imported after loading: the report screens read the tenant's role lists when their modules evaluate.
const { App } = await import('@/app/App')

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
