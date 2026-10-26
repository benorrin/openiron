import { useState, type FormEvent } from 'react'
import { useBodyWeight, useDeleteBodyWeight, useLogBodyWeight } from '../api/hooks'
import { LineChart } from '../components/charts/LineChart'
import { RangeFilter } from '../components/RangeFilter'
import { RANGES, withinRange } from '../lib/range'
import { formatDate, formatKg, formatShortDate, parseDate, todayIso } from '../lib/format'

const HISTORY_PREVIEW = 10

export function BodyWeightPage() {
  const { data: entries = [], isLoading, error } = useBodyWeight()
  const log = useLogBodyWeight()
  const remove = useDeleteBodyWeight()

  const [range, setRange] = useState(RANGES[1])
  const [date, setDate] = useState(todayIso)
  const [weight, setWeight] = useState('')
  const [showAll, setShowAll] = useState(false)

  async function submit(e: FormEvent) {
    e.preventDefault()
    await log.mutateAsync({ date, weightKg: Number(weight) })
    setWeight('')
  }

  const visible = withinRange(entries, range, (e) => parseDate(e.date).getTime())
  const first = visible[0]
  const last = visible[visible.length - 1]
  const change = first && last && first !== last ? last.weightKg - first.weightKg : null

  return (
    <div className="page">
      <h1>Body weight</h1>

      <form className="card row" onSubmit={submit} style={{ alignItems: 'flex-end' }}>
        <label className="field grow">
          Date
          <input type="date" value={date} max={todayIso()} onChange={(e) => setDate(e.target.value)} required />
        </label>
        <label className="field grow">
          Weight (kg)
          <input
            type="number"
            inputMode="decimal"
            step="0.1"
            min={20}
            max={500}
            value={weight}
            onChange={(e) => setWeight(e.target.value)}
            required
          />
        </label>
        <button className="btn primary" disabled={log.isPending}>
          Log weight
        </button>
        {log.error && <p className="error grow" role="alert">{log.error.message}</p>}
      </form>

      {isLoading && <p className="empty">Loading…</p>}
      {error && <p className="error">{error.message}</p>}
      {!isLoading && entries.length === 0 && (
        <div className="card empty">
          <p>No weigh-ins yet. Log your first one above to start the chart.</p>
        </div>
      )}

      {entries.length > 0 && (
        <>
          <RangeFilter value={range} onChange={setRange} />
          {visible.length === 0 ? (
            <div className="card empty">
              <p>No weigh-ins in the last {range.label}.</p>
            </div>
          ) : (
            <>
              <div className="tiles">
                <div className="card tile">
                  <div className="tile-label">Latest</div>
                  <div className="tile-value">{formatKg(last.weightKg)} kg</div>
                  <div className="tile-sub">{formatDate(last.date)}</div>
                </div>
                <div className="card tile">
                  <div className="tile-label">Change over {range.label}</div>
                  <div className="tile-value">
                    {change === null ? '–' : `${change > 0 ? '+' : change < 0 ? '−' : ''}${formatKg(Math.abs(change))} kg`}
                  </div>
                  <div className="tile-sub">{visible.length} weigh-ins</div>
                </div>
              </div>
              <section className="card">
                <div className="card-title">
                  <h2>Body weight</h2>
                  <span className="muted">kg</span>
                </div>
                <LineChart
                  ariaLabel="Body weight"
                  unit="kg"
                  area
                  formatX={(x) => formatShortDate(new Date(x).toISOString())}
                  series={[
                    {
                      id: 'weight',
                      label: 'Body weight',
                      color: 'var(--series-1)',
                      points: visible.map((e) => ({ x: parseDate(e.date).getTime(), y: e.weightKg })),
                    },
                  ]}
                />
              </section>
            </>
          )}

          <section className="card">
            <h2 className="card-title">History</h2>
            <ul className="list">
              {[...entries].reverse().slice(0, showAll ? undefined : HISTORY_PREVIEW).map((e) => (
                <li key={e.id} className="row" style={{ padding: '8px 4px', justifyContent: 'space-between' }}>
                  <span>{formatDate(e.date)}</span>
                  <span className="row">
                    <strong>{formatKg(e.weightKg)} kg</strong>
                    <button
                      className="btn small ghost danger"
                      onClick={() => confirm(`Delete the ${formatDate(e.date)} entry?`) && remove.mutate(e.id)}
                      aria-label={`Delete entry for ${formatDate(e.date)}`}
                    >
                      Delete
                    </button>
                  </span>
                </li>
              ))}
            </ul>
            {entries.length > HISTORY_PREVIEW && (
              <button className="btn small ghost" onClick={() => setShowAll((v) => !v)}>
                {showAll ? 'Show fewer' : `Show all ${entries.length}`}
              </button>
            )}
          </section>
        </>
      )}
    </div>
  )
}
