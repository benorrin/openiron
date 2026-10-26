import { Link, useNavigate } from 'react-router-dom'
import { useTemplates, useWorkouts } from '../api/hooks'
import { formatDate, formatVolume } from '../lib/format'
import { TrainingTabs } from '../components/SectionTabs'
import { PageSkeleton } from '../components/Skeleton'
import { EmptyState } from '../components/EmptyState'

export function WorkoutsPage() {
  const { data: workouts, isLoading, error } = useWorkouts()
  const { data: templates = [] } = useTemplates()
  const navigate = useNavigate()

  return (
    <div className="page">
      <TrainingTabs />
      <div className="page-header">
        <h1>Workouts</h1>
        <div className="row">
          {templates.length > 0 && (
            <select
              aria-label="Start from template"
              value=""
              style={{ width: 'auto' }}
              onChange={(e) => e.target.value && navigate(`/workouts/new?template=${e.target.value}`)}
            >
              <option value="">Start from template…</option>
              {templates.map((t) => (
                <option key={t.id} value={t.id}>
                  {t.name}
                </option>
              ))}
            </select>
          )}
          <Link to="/workouts/new" className="btn primary">
            Empty workout
          </Link>
        </div>
      </div>

      {isLoading && <PageSkeleton />}
      {error && <p className="error">{error.message}</p>}
      {workouts?.length === 0 && (
        <EmptyState icon="dumbbell" title="Nothing logged yet">
          Start a workout from a template, or log an empty one.
        </EmptyState>
      )}

      {workouts && workouts.length > 0 && (
        <ul className="card list">
          {workouts.map((w) => (
            <li key={w.id}>
              <Link to={`/workouts/${w.id}`} className="list-link">
                <div>
                  <strong>{w.name}</strong>
                  <p className="muted">
                    {formatDate(w.performedAt)} · {w.exerciseCount} exercises · {w.setCount} sets
                  </p>
                </div>
                <span className="pill">{formatVolume(w.totalVolumeKg)} kg</span>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
