import { Link } from 'react-router-dom'
import type { DayState, WeekDay } from '../api/consistency'
import { Icon, type IconName } from './Icon'

const ICON: Record<DayState, IconName> = {
  done: 'check',
  missed: 'x',
  pending: 'plus',
  upcoming: 'minus',
  rest: 'minus',
  bonus: 'bolt',
}

const LABEL: Record<DayState, string> = {
  done: 'done',
  missed: 'missed',
  pending: 'still to do today',
  upcoming: 'coming up',
  rest: 'rest day',
  bonus: 'extra session',
}

const weekday = new Intl.DateTimeFormat(undefined, { weekday: 'short' })
const longDate = new Intl.DateTimeFormat(undefined, { weekday: 'long', day: 'numeric', month: 'long' })

/** Monday to Sunday, each day set against the plan. State is shown by icon and word as well as colour. */
export function WeekStrip({ week }: { week: WeekDay[] }) {
  return (
    <ul className="week-strip">
      {week.map((d) => {
        const date = new Date(`${d.date}T00:00:00`)
        const summary = `${longDate.format(date)}: ${LABEL[d.state]}${d.templateName ? `, ${d.templateName}` : ''}`
        return (
          <li key={d.date} className={`week-day ${d.state}${d.isToday ? ' today' : ''}`} aria-label={summary}>
            <span className="week-day-name">{weekday.format(date)}</span>
            <span className="week-day-badge">
              <Icon name={ICON[d.state]} size={18} />
            </span>
            <span className="week-day-note">
              {d.templateName ?? (d.state === 'bonus' ? 'Extra' : d.state === 'done' ? 'Done' : '')}
            </span>
            {d.state === 'pending' && (
              <Link className="week-day-start" to={d.templateId ? `/workouts/new?template=${d.templateId}` : '/workouts/new'}>
                Start
              </Link>
            )}
          </li>
        )
      })}
    </ul>
  )
}
