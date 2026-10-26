import { Link } from 'react-router-dom'
import { useOverview, useTemplates, useWorkouts } from '../api/hooks'
import { BarChart } from '../components/charts/BarChart'
import { formatDate, formatKg, formatShortDate, formatVolume } from '../lib/format'

export function HomePage() {
  const { data: overview, isLoading } = useOverview()
  const { data: templates = [] } = useTemplates()
  const { data: workouts = [] } = useWorkouts()

  if (isLoading || !overview) return <p className="empty">Loading…</p>

  const change = overview.bodyWeightChangeKg

  return (
    <div className="page">
      <div className="page-header">
        <h1>Home</h1>
        <Link to="/workouts/new" className="btn primary">
          Log workout
        </Link>
      </div>

      <div className="tiles">
        <div className="card tile">
          <div className="tile-label">Workouts, last 30 days</div>
          <div className="tile-value">{overview.workoutsLast30Days}</div>
          <div className="tile-sub">{overview.totalWorkouts} in total</div>
        </div>
        <div className="card tile">
          <div className="tile-label">Body weight</div>
          <div className="tile-value">
            {overview.latestBodyWeightKg === null ? '–' : `${formatKg(overview.latestBodyWeightKg)} kg`}
          </div>
          <div className="tile-sub">
            {change === null
              ? 'Log a weigh-in to track it'
              : `${change > 0 ? '+' : change < 0 ? '−' : ''}${formatKg(Math.abs(change))} kg since first entry`}
          </div>
        </div>
      </div>

      <section className="card">
        <div className="card-title">
          <h2>Weekly volume</h2>
          <span className="muted">kg lifted, last 12 weeks</span>
        </div>
        <BarChart
          ariaLabel="Weekly volume"
          unit="kg"
          color="var(--series-1)"
          bars={overview.weeks.map((w) => ({
            key: w.weekStart,
            label: formatShortDate(w.weekStart),
            title: `Week of ${formatDate(w.weekStart)} · ${w.workouts} ${w.workouts === 1 ? 'workout' : 'workouts'}`,
            value: w.volumeKg,
          }))}
        />
      </section>

      <div className="two-col">
        <section className="card">
          <h2 className="card-title">Start a workout</h2>
          {templates.length === 0 ? (
            <p className="muted">
              Save a routine as a <Link to="/templates/new">template</Link> to start it in one tap.
            </p>
          ) : (
            <ul className="list">
              {templates.map((t) => (
                <li key={t.id}>
                  <Link to={`/workouts/new?template=${t.id}`} className="list-link">
                    <strong>{t.name}</strong>
                    <span className="muted">{t.exercises.length} exercises</span>
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </section>

        <section className="card">
          <h2 className="card-title">Recent workouts</h2>
          {workouts.length === 0 ? (
            <p className="muted">Nothing logged yet.</p>
          ) : (
            <ul className="list">
              {workouts.slice(0, 4).map((w) => (
                <li key={w.id}>
                  <Link to={`/workouts/${w.id}`} className="list-link">
                    <div>
                      <strong>{w.name}</strong>
                      <p className="muted">{formatDate(w.performedAt)}</p>
                    </div>
                    <span className="pill">{formatVolume(w.totalVolumeKg)} kg</span>
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>
    </div>
  )
}
