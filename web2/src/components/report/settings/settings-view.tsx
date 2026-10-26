import { useEditor, type SettingsSectionId } from '@/lib/report/store'
import { cn } from '@/lib/utils'
import { ProfilesSection } from './profiles-section'
import { PublishSection } from './publish-section'
import { DimensionsSection } from './dimensions-section'
import { ScenariosSection } from './scenarios-section'
import { DefaultsSection } from './settings-sections'
import { VariableScopesSection } from './variable-scopes-section'

const SECTIONS: Record<SettingsSectionId, { label: string; description: string }> = {
  defaults: { label: 'Defaults', description: 'What the report shows when someone first opens it.' },
  profiles: { label: 'Profiles', description: 'Which source each profile reads a variable from, when more than one source supplies it.' },
  breakout: { label: 'Dimensions', description: 'Dimensions the report can be grouped or filtered by.' },
  scenarios: {
    label: 'Scenarios',
    description:
      'A scenario is a dimension role whose members a run chooses one of, such as a plan version or a rate shock. A run or comparison names one member of each scenario its sources read; sources that do not read it are unaffected.',
  },
  scopes: { label: 'Variable scopes', description: 'Which rows a variable reads data for.' },
  publish: { label: 'Diagnostics', description: 'Checks that must pass before this report can be enabled.' },
}

const WIDE = new Set<SettingsSectionId>(['profiles', 'breakout', 'scenarios', 'scopes', 'publish'])

export function SettingsView() {
  const { ui } = useEditor()
  const key = ui.settingsSection
  const current = SECTIONS[key]

  return (
    <section aria-labelledby="settings-heading" className="min-h-0 flex-1 overflow-y-auto">
      <div className={cn('flex w-full flex-col gap-6 p-4 md:p-6', !WIDE.has(key) && 'mx-auto max-w-3xl')}>
        <header className="flex flex-col gap-0.5">
          <h2 id="settings-heading" className="text-lg font-semibold">
            {current.label}
          </h2>
          <p className="text-muted-foreground">{current.description}</p>
        </header>
        {key === 'defaults' && <DefaultsSection />}
        {key === 'profiles' && <ProfilesSection />}
        {key === 'breakout' && <DimensionsSection />}
        {key === 'scenarios' && <ScenariosSection />}
        {key === 'scopes' && <VariableScopesSection />}
        {key === 'publish' && <PublishSection />}
      </div>
    </section>
  )
}
