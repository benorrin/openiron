import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useExerciseProgress, useProgressExercises } from '../api/hooks'
import { BarChart } from '../components/charts/BarChart'
import { LineChart } from '../components/charts/LineChart'
import { RangeFilter } from '../components/RangeFilter'
import { RANGES, withinRange } from '../lib/range'
import { formatDate, formatKg, formatShortDate, formatVolume } from '../lib/format'

export function ProgressPage() {
  const { data: exercises, isLoading } = useProgressExercises()
  const [picked, setPicked] = useState<string>()
  const [range, setRange] = useState(RANGES[3])

  // Default to the exercise trained most recently.
  const exerciseId = picked ?? exercises?.[0]?.exerciseId
  const { data: progress } = useExerciseProgress(exerciseId)

  if (isLoading) return <p className="empty">Loading…</p>

  if (!exercises || exercises.length === 0) {
    return (
      <div className="page">
        <h1>Progress</h1>
        <div className="card empty stack">
          <p>Charts appear once you've logged a workout.</p>
          <div>
            <Link to="/workouts/new" className="btn primary">
              Log a workout
            </Link>
          </div>
        </div>
      </div>
    )
  }

  const points = progress ? withinRange(progress.points, range, (p) => new Date(p.performedAt).getTime()) : []
  const xOf = (iso: string) => new Date(iso).getTime()
  const dateLabel = (x: number) => formatShortDate(new Date(x).toISOString())

  return (
    <div className="page">
      <h1>Progress</h1>

      <div className="row">
        <select aria-label="Exercise" value={exerciseId} onChange={(e) => setPicked(e.target.value)} style={{ width: 'auto', maxWidth: '100%' }}>
          {exercises.map((e) => (
            <option key={e.exerciseId} value={e.exerciseId}>
              {e.name} ({e.sessions})
            </option>
          ))}
        </select>
        <RangeFilter value={range} onChange={setRange} />
      </div>

      {progress && (
        <>
          <div className="tiles">
            <div className="card tile">
              <div className="tile-label">Heaviest set</div>
              <div className="tile-value">{formatKg(progress.records.heaviestWeightKg)} kg</div>
              <div className="tile-sub">{progress.records.heaviestOn ? formatDate(progress.records.heaviestOn) : ''}</div>
            </div>
            <div className="card tile">
              <div className="tile-label">Best estimated 1RM</div>
              <div className="tile-value">{formatKg(progress.records.best1RmKg)} kg</div>
              <div className="tile-sub">{progress.records.best1RmOn ? formatDate(progress.records.best1RmOn) : ''}</div>
            </div>
            <div className="card tile">
              <div className="tile-label">Sessions</div>
              <div className="tile-value">{progress.points.length}</div>
              <div className="tile-sub">{points.length} in range</div>
            </div>
          </div>

          {points.length === 0 ? (
            <div className="card empty">
              <p>No sessions of {progress.name} in the last {range.label}.</p>
            </div>
          ) : (
            <>
              <section className="card">
                <div className="card-title">
                  <h2>{progress.name} strength</h2>
                  <span className="muted">kg per session</span>
                </div>
                <LineChart
                  ariaLabel={`${progress.name} strength`}
                  unit="kg"
                  formatX={dateLabel}
                  series={[
                    {
                      id: 'e1rm',
                      label: 'Estimated 1RM',
                      color: 'var(--series-1)',
                      points: points.map((p) => ({ x: xOf(p.performedAt), y: p.estimated1RmKg })),
                    },
                    {
                      id: 'top',
                      label: 'Top set weight',
                      color: 'var(--series-2)',
                      points: points.map((p) => ({ x: xOf(p.performedAt), y: p.topWeightKg })),
                    },
                  ]}
                />
                <p className="muted" style={{ marginTop: 8 }}>
                  Estimated 1RM uses the Epley formula: weight × (1 + reps ÷ 30).
                </p>
              </section>

              <section className="card">
                <div className="card-title">
                  <h2>{progress.name} volume</h2>
                  <span className="muted">kg lifted per session (sets × reps × weight)</span>
                </div>
                <BarChart
                  ariaLabel={`${progress.name} volume`}
                  unit="kg"
                  color="var(--series-1)"
                  bars={points.map((p) => ({
                    key: p.workoutId,
                    label: formatShortDate(p.performedAt),
                    title: `${formatDate(p.performedAt)} · ${p.totalReps} reps`,
                    value: p.volumeKg,
                  }))}
                />
                <p className="muted" style={{ marginTop: 8 }}>
                  Latest: {formatVolume(points[points.length - 1].volumeKg)} kg
                </p>
              </section>
            </>
          )}
        </>
      )}
    </div>
  )
}
