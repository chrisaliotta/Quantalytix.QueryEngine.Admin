import { cn } from '@/lib/utils'
import { useReportEditor, type SettingsSection } from '../editor-state'
import { DefaultsSection } from './defaults-section'
import { DiagnosticsSection } from './diagnostics-section'
import { DimensionsSection } from './dimensions-section'
import { ProfilesSection } from './profiles-section'
import { ScenariosSection } from './scenarios-section'
import { ScopesSection } from './scopes-section'

const SECTIONS: Record<SettingsSection, { label: string; description: string }> = {
  defaults: { label: 'Defaults', description: 'What the report shows when a request names no profile, breakout or filter.' },
  profiles: { label: 'Profiles', description: 'Which source each profile reads a variable from, when more than one source supplies it.' },
  dimensions: { label: 'Dimensions', description: 'Roles the report can be broken out or filtered by, grouped as each says.' },
  scenarios: {
    label: 'Scenarios',
    description: 'A scenario is a role whose members a run chooses one of, such as a plan version. Its name is the role\'s.',
  },
  scopes: { label: 'Variable scopes', description: 'The member row whose branch a variable reads; a variable without a scope reads every row.' },
  diagnostics: { label: 'Diagnostics', description: 'The report rules, which must all pass before the report can be enabled.' },
}

const WIDE = new Set<SettingsSection>(['profiles', 'dimensions', 'diagnostics'])

export function SettingsView() {
  const { ui } = useReportEditor()
  const current = SECTIONS[ui.section]
  return (
    <section aria-labelledby="settings-heading" className="min-h-0 flex-1 overflow-y-auto">
      <div className={cn('flex w-full flex-col gap-6 p-4 md:p-6', !WIDE.has(ui.section) && 'mx-auto max-w-3xl')}>
        <header className="flex flex-col gap-0.5">
          <h2 id="settings-heading" className="text-lg font-semibold">
            {current.label}
          </h2>
          <p className="text-muted-foreground">{current.description}</p>
        </header>
        {ui.section === 'defaults' && <DefaultsSection />}
        {ui.section === 'profiles' && <ProfilesSection />}
        {ui.section === 'dimensions' && <DimensionsSection />}
        {ui.section === 'scenarios' && <ScenariosSection />}
        {ui.section === 'scopes' && <ScopesSection />}
        {ui.section === 'diagnostics' && <DiagnosticsSection />}
      </div>
    </section>
  )
}
