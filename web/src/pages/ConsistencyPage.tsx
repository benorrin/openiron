import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useConsistency, useSchedule, type Consistency } from '../api/consistency'
import { BarChart } from '../components/charts/BarChart'
import { Heatmap } from '../components/Heatmap'
import { Icon } from '../components/Icon'
import { ScheduleEditor } from '../components/ScheduleEditor'
import { ProgressTabs } from '../components/SectionTabs'
import { PageSkeleton } from '../components/Skeleton'
import { WeekStrip } from '../components/WeekStrip'
import { formatDate, formatShortDate } from '../lib/format'

export function ConsistencyPage() {
  const { data, isLoading, error } = useConsistency()

  if (isLoading) return <PageSkeleton />
  if (!data) {
    return (
      <div className="page">
        <ProgressTabs />
        <p className="error">{error?.message ?? 'Something went wrong.'}</p>
      </div>
    )
  }

  return (
    <div className="page">
      <ProgressTabs />
      <h1>Consistency</h1>
      <WeekCard data={data} />
      <Tiles data={data} />
      <section className="card">
        <div className="card-title">
          <h2>The last year</h2>
          <span className="muted">
            {data.summary.activeDays365} active {data.summary.activeDays365 === 1 ? 'day' : 'days'}
          </span>
        </div>
        <Heatmap start={data.heatmapStart} today={data.today} cells={data.heatmap} stepGoal={data.stepGoal} />
        <p className="muted" style={{ marginTop: 8 }}>
          A day lights up for each workout, each logged activity, and for reaching your step goal ({data.stepGoal.toLocaleString()}).
        </p>
      </section>
      {data.hasSchedule && <AdherenceCard data={data} />}
      <ScheduleCard hasSchedule={data.hasSchedule} />
    </div>
  )
}

export function WeekCard({ data, compact = false }: { data: Consistency; compact?: boolean }) {
  if (!data.hasSchedule) {
    return (
      <section className="card stack">
        <div>
          <h2>Plan your week</h2>
          <p className="muted" style={{ marginTop: 4 }}>
            Choose the days you train and we'll show how you're keeping to them, week by week.
          </p>
        </div>
        <div>
          <Link to="/consistency" className="btn primary">
            <Icon name="calendar" size={18} />
            Set a schedule
          </Link>
        </div>
      </section>
    )
  }

  const current = data.weeks[data.weeks.length - 1]
  return (
    <section className="card stack">
      <div className="card-title" style={{ marginBottom: 0 }}>
        <h2>This week</h2>
        {compact ? (
          <Link to="/consistency">Consistency</Link>
        ) : (
          <span className="muted">
            {current.completed} of {Math.max(current.due, current.completed)} done so far, {current.scheduled} planned
          </span>
        )}
      </div>
      <WeekStrip week={data.week} />
    </section>
  )
}

function Tiles({ data }: { data: Consistency }) {
  const { summary } = data
  return (
    <div className="tiles">
      {data.hasSchedule && (
        <>
          <div className="card tile">
            <span className="icon-chip"><Icon name="flame" size={18} /></span>
            <div className="tile-label">Current streak</div>
            <div className="tile-value">{summary.streak.current}</div>
            <div className="tile-sub">scheduled days in a row</div>
          </div>
          <div className="card tile">
            <span className="icon-chip"><Icon name="trending" size={18} /></span>
            <div className="tile-label">Best streak</div>
            <div className="tile-value">{summary.streak.best}</div>
            <div className="tile-sub">in the last year</div>
          </div>
          <div className="card tile">
            <span className="icon-chip"><Icon name="check" size={18} /></span>
            <div className="tile-label">Last 4 weeks</div>
            <div className="tile-value">{summary.adherencePercent4Weeks === null ? '–' : `${summary.adherencePercent4Weeks}%`}</div>
            <div className="tile-sub">of scheduled days done</div>
          </div>
        </>
      )}
      <div className="card tile">
        <span className="icon-chip"><Icon name="calendar" size={18} /></span>
        <div className="tile-label">Active days</div>
        <div className="tile-value">{summary.activeDays30}</div>
        <div className="tile-sub">in the last 30 days</div>
      </div>
    </div>
  )
}

function AdherenceCard({ data }: { data: Consistency }) {
  if (!data.weeks.some((w) => w.percent !== null)) return null

  return (
    <section className="card">
      <div className="card-title">
        <h2>Weekly adherence</h2>
        <span className="muted">% of scheduled days done</span>
      </div>
      <BarChart
        ariaLabel="Weekly adherence"
        unit="%"
        color="var(--series-1)"
        bars={data.weeks.map((w) => ({
          key: w.weekStart,
          label: formatShortDate(w.weekStart),
          title:
            w.due === 0
              ? `Week of ${formatDate(w.weekStart)} · nothing scheduled yet`
              : `Week of ${formatDate(w.weekStart)} · ${w.completed} of ${w.due} done`,
          value: w.percent ?? 0,
        }))}
      />
    </section>
  )
}

function ScheduleCard({ hasSchedule }: { hasSchedule: boolean }) {
  const { data: schedule } = useSchedule()
  const [open, setOpen] = useState(!hasSchedule)

  if (!schedule) return null
  return (
    <details className="card disclosure" open={open} onToggle={(e) => setOpen(e.currentTarget.open)}>
      <summary>Training schedule</summary>
      <ScheduleEditor key={JSON.stringify(schedule)} schedule={schedule} onSaved={() => setOpen(false)} />
    </details>
  )
}
