import { NavLink } from 'react-router-dom'

interface Props {
  label: string
  items: { to: string; label: string }[]
}

/**
 * Some phone tabs cover two pages (the bar only has room for six), so this switches between them at the top of
 * each. Hidden on wide screens, where the sidebar lists every page.
 */
function SectionTabs({ label, items }: Props) {
  return (
    <nav className="tabs section-tabs" aria-label={label}>
      {items.map((i) => (
        <NavLink key={i.to} to={i.to}>
          {i.label}
        </NavLink>
      ))}
    </nav>
  )
}

export const BodyTabs = () => (
  <SectionTabs label="Body" items={[{ to: '/body-weight', label: 'Weight' }, { to: '/nutrition', label: 'Nutrition' }]} />
)

export const TrainingTabs = () => (
  <SectionTabs label="Training" items={[{ to: '/workouts', label: 'Workouts' }, { to: '/activity', label: 'Activity' }]} />
)

export const ProgressTabs = () => (
  <SectionTabs label="Progress" items={[{ to: '/progress', label: 'Progress' }, { to: '/consistency', label: 'Consistency' }]} />
)
