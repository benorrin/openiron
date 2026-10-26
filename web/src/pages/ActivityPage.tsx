import { useMemo, useState, type FormEvent } from 'react'
import {
  useActivities,
  useDeleteActivity,
  useLogActivity,
  useSchedule,
  type Activity,
  type ActivityType,
} from '../api/consistency'
import { BarChart } from '../components/charts/BarChart'
import { EmptyState } from '../components/EmptyState'
import { Icon } from '../components/Icon'
import { TrainingTabs } from '../components/SectionTabs'
import { PageSkeleton } from '../components/Skeleton'
import { formatDate, formatShortDate, todayIso } from '../lib/format'

const TYPES: { value: ActivityType; label: string }[] = [
  { value: 'walk', label: 'Walk' },
  { value: 'run', label: 'Run' },
  { value: 'treadmill', label: 'Treadmill' },
  { value: 'cycling', label: 'Cycling' },
  { value: 'swim', label: 'Swim' },
  { value: 'hike', label: 'Hike' },
  { value: 'other', label: 'Other' },
  { value: 'steps', label: 'Daily steps' },
]

const typeLabel = (t: ActivityType) => TYPES.find((x) => x.value === t)?.label ?? t

function describe(a: Activity): string {
  if (a.type === 'steps') return `${a.steps?.toLocaleString() ?? 0} steps`
  const parts = [`${a.durationMinutes} min`]
  if (a.distanceKm) parts.push(`${a.distanceKm} km`)
  return parts.join(' · ')
}

export function ActivityPage() {
  const { data: activities, isLoading, error } = useActivities()
  const { data: schedule } = useSchedule()
  const goal = schedule?.stepGoal ?? 8000

  if (isLoading) return <PageSkeleton />

  return (
    <div className="page">
      <TrainingTabs />
      <h1>Activity</h1>
      {error && <p className="error">{error.message}</p>}
      <TodayCard activities={activities ?? []} goal={goal} />
      <LogForm />
      <StepsChart activities={activities ?? []} goal={goal} />
      <History activities={activities ?? []} />
    </div>
  )
}

function TodayCard({ activities, goal }: { activities: Activity[]; goal: number }) {
  const [today] = useState(todayIso)
  const steps = activities.find((a) => a.date === today && a.type === 'steps')?.steps ?? 0
  const minutes = activities.filter((a) => a.date === today && a.type !== 'steps').reduce((sum, a) => sum + (a.durationMinutes ?? 0), 0)
  const percent = Math.min(100, Math.round((steps / goal) * 100))

  return (
    <div className="tiles">
      <div className="card tile">
        <span className="icon-chip"><Icon name="pulse" size={18} /></span>
        <div className="tile-label">Steps today</div>
        <div className="tile-value">{steps.toLocaleString()}</div>
        <div className="progress-bar" role="progressbar" aria-valuenow={percent} aria-valuemin={0} aria-valuemax={100} aria-label="Progress to step goal" style={{ margin: '6px 0 2px' }}>
          <span style={{ width: `${percent}%` }} />
        </div>
        <div className="tile-sub">{steps >= goal ? 'Goal reached' : `${percent}% of ${goal.toLocaleString()}`}</div>
      </div>
      <div className="card tile">
        <span className="icon-chip"><Icon name="flame" size={18} /></span>
        <div className="tile-label">Active minutes today</div>
        <div className="tile-value">{minutes}</div>
        <div className="tile-sub">walks, runs and other sessions</div>
      </div>
    </div>
  )
}

function LogForm() {
  const log = useLogActivity()
  const [type, setType] = useState<ActivityType>('walk')
  const [date, setDate] = useState(todayIso)
  const [minutes, setMinutes] = useState('')
  const [distance, setDistance] = useState('')
  const [steps, setSteps] = useState('')
  const [notes, setNotes] = useState('')
  const isSteps = type === 'steps'

  async function submit(e: FormEvent) {
    e.preventDefault()
    try {
      await log.mutateAsync({
        date,
        type,
        durationMinutes: isSteps ? null : Number(minutes),
        distanceKm: !isSteps && distance ? Number(distance) : null,
        steps: isSteps ? Number(steps) : null,
        notes: notes.trim() || null,
      })
      setMinutes('')
      setDistance('')
      setSteps('')
      setNotes('')
    } catch {
      // Shown from log.error.
    }
  }

  return (
    <form className="card stack" onSubmit={submit}>
      <h2>Log activity</h2>
      <div className="row">
        <label className="field grow">
          What
          <select value={type} onChange={(e) => setType(e.target.value as ActivityType)}>
            {TYPES.map((t) => (
              <option key={t.value} value={t.value}>
                {t.label}
              </option>
            ))}
          </select>
        </label>
        <label className="field grow">
          Date
          <input type="date" value={date} max={todayIso()} onChange={(e) => setDate(e.target.value)} required />
        </label>
      </div>
      {isSteps ? (
        <label className="field">
          Steps
          <input type="number" inputMode="numeric" min={1} max={200000} value={steps} onChange={(e) => setSteps(e.target.value)} required />
          <span className="muted">One count per day: logging a day again replaces it.</span>
        </label>
      ) : (
        <div className="row">
          <label className="field grow">
            Duration (min)
            <input type="number" inputMode="numeric" min={1} max={1440} value={minutes} onChange={(e) => setMinutes(e.target.value)} required />
          </label>
          <label className="field grow">
            Distance (km, optional)
            <input type="number" inputMode="decimal" step="0.1" min={0} max={1000} value={distance} onChange={(e) => setDistance(e.target.value)} />
          </label>
        </div>
      )}
      <label className="field">
        Notes (optional)
        <input value={notes} onChange={(e) => setNotes(e.target.value)} maxLength={500} />
      </label>
      {log.error && <p className="error" role="alert">{log.error.message}</p>}
      <div>
        <button className="btn primary" disabled={log.isPending}>
          <Icon name="plus" size={18} />
          Log {isSteps ? 'steps' : typeLabel(type).toLowerCase()}
        </button>
      </div>
    </form>
  )
}

function StepsChart({ activities, goal }: { activities: Activity[]; goal: number }) {
  const [end] = useState(() => new Date())
  const bars = useMemo(() => {
    const byDate = new Map(activities.filter((a) => a.type === 'steps').map((a) => [a.date, a.steps ?? 0]))
    if (byDate.size === 0) return []
    const out: { key: string; label: string; title: string; value: number }[] = []
    for (let i = 29; i >= 0; i--) {
      const d = new Date(end)
      d.setDate(d.getDate() - i)
      const key = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
      out.push({ key, label: formatShortDate(key), title: formatDate(key), value: byDate.get(key) ?? 0 })
    }
    return out
  }, [activities, end])

  if (bars.length === 0) return null
  return (
    <section className="card">
      <div className="card-title">
        <h2>Daily steps</h2>
        <span className="muted">last 30 days · goal {goal.toLocaleString()}</span>
      </div>
      <BarChart ariaLabel="Daily steps" unit="steps" color="var(--series-1)" bars={bars} />
    </section>
  )
}

const HISTORY_PREVIEW = 15

function History({ activities }: { activities: Activity[] }) {
  const remove = useDeleteActivity()
  const [showAll, setShowAll] = useState(false)

  if (activities.length === 0) {
    return (
      <EmptyState icon="pulse" title="No activity logged yet">
        Walks, runs, treadmill sessions and your daily steps all count toward your consistency.
      </EmptyState>
    )
  }

  return (
    <section className="card">
      <h2 className="card-title">History</h2>
      <ul className="list">
        {activities.slice(0, showAll ? undefined : HISTORY_PREVIEW).map((a) => (
          <li key={a.id} className="row" style={{ padding: '10px 4px', justifyContent: 'space-between', flexWrap: 'nowrap' }}>
            <div>
              <strong>{typeLabel(a.type)}</strong> <span className="muted">{describe(a)}</span>
              <p className="muted">
                {formatDate(a.date)}
                {a.notes && ` · ${a.notes}`}
              </p>
            </div>
            <button
              className="btn small ghost danger"
              aria-label={`Delete ${typeLabel(a.type)} on ${formatDate(a.date)}`}
              disabled={remove.isPending}
              onClick={() => confirm('Delete this entry?') && remove.mutate(a.id)}
            >
              <Icon name="trash" size={16} />
            </button>
          </li>
        ))}
      </ul>
      {activities.length > HISTORY_PREVIEW && (
        <button className="btn small ghost" onClick={() => setShowAll((v) => !v)}>
          {showAll ? 'Show fewer' : `Show all ${activities.length}`}
        </button>
      )}
      {remove.error && <p className="error" role="alert">{remove.error.message}</p>}
    </section>
  )
}
