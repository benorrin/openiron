import { useEffect, useMemo, useRef, useState, type KeyboardEvent, type PointerEvent } from 'react'
import type { HeatCell } from '../api/consistency'
import { buildHeatGrid, describeCell, monthlyActiveDays } from '../lib/heatmap'
import { ChartTable } from './charts/ChartTable'
import { Tooltip } from './charts/Tooltip'

const CELL = 12
const GAP = 3
const STEP = CELL + GAP
const LEFT = 30
const TOP = 20
const ROW_LABELS = [{ row: 0, text: 'Mon' }, { row: 2, text: 'Wed' }, { row: 4, text: 'Fri' }]

const dateFormat = new Intl.DateTimeFormat(undefined, { weekday: 'short', day: 'numeric', month: 'short', year: 'numeric' })

interface Props {
  start: string
  today: string
  cells: HeatCell[]
  stepGoal: number
}

/**
 * A contribution graph: a column per week, a row per weekday, darker for busier days. Wider than a phone, so it
 * scrolls sideways, starting at the most recent week.
 */
export function Heatmap({ start, today, cells, stepGoal }: Props) {
  const grid = useMemo(() => buildHeatGrid(start, today, cells), [start, today, cells])
  const scroller = useRef<HTMLDivElement>(null)
  const [active, setActive] = useState<{ week: number; row: number } | null>(null)
  const [showTable, setShowTable] = useState(false)

  useEffect(() => {
    const el = scroller.current
    if (el) el.scrollLeft = el.scrollWidth
  }, [grid])

  const width = LEFT + grid.weeks.length * STEP
  const height = TOP + 7 * STEP
  const activeDays = cells.filter((c) => c.level > 0).length
  const activeDay = active ? grid.weeks[active.week]?.[active.row] ?? null : null

  function cellAt(e: PointerEvent<SVGSVGElement>) {
    const rect = e.currentTarget.getBoundingClientRect()
    const week = Math.floor((e.clientX - rect.left - LEFT) / STEP)
    const row = Math.floor((e.clientY - rect.top - TOP) / STEP)
    const day = grid.weeks[week]?.[row]
    setActive(day ? { week, row } : null)
  }

  function onKey(e: KeyboardEvent<SVGSVGElement>) {
    const moves: Record<string, [number, number]> = { ArrowLeft: [-1, 0], ArrowRight: [1, 0], ArrowUp: [0, -1], ArrowDown: [0, 1] }
    const move = moves[e.key]
    if (!move) return
    e.preventDefault()
    const lastWeek = grid.weeks.length - 1
    const current = active ?? { week: lastWeek, row: grid.weeks[lastWeek].filter(Boolean).length - 1 }
    const week = Math.min(Math.max(current.week + move[0], 0), lastWeek)
    const row = Math.min(Math.max(current.row + move[1], 0), 6)
    if (grid.weeks[week]?.[row]) setActive({ week, row })
  }

  const tip = activeDay ? describeCell(activeDay.cell, stepGoal) : null

  return (
    <div className="chart">
      <div className="heatmap-scroll" ref={scroller}>
        <div className="chart-plot" style={{ width }}>
          <svg
            width={width}
            height={height}
            role="img"
            aria-label={`${activeDays} active days in the last year`}
            tabIndex={0}
            onPointerMove={cellAt}
            onPointerDown={cellAt}
            onPointerLeave={() => setActive(null)}
            onKeyDown={onKey}
            onFocus={() => setActive((a) => a ?? { week: grid.weeks.length - 1, row: Math.max(grid.weeks[grid.weeks.length - 1].filter(Boolean).length - 1, 0) })}
            onBlur={() => setActive(null)}
          >
            {grid.months.map((m) => (
              <text key={`${m.week}-${m.label}`} className="chart-label" x={LEFT + m.week * STEP} y={11}>
                {m.label}
              </text>
            ))}
            {ROW_LABELS.map((r) => (
              <text key={r.text} className="chart-label" x={0} y={TOP + r.row * STEP + CELL - 1}>
                {r.text}
              </text>
            ))}
            {grid.weeks.map((week, w) =>
              week.map((day, r) =>
                day ? (
                  <rect
                    key={day.date}
                    className={`heat heat-${day.level}`}
                    x={LEFT + w * STEP}
                    y={TOP + r * STEP}
                    width={CELL}
                    height={CELL}
                    rx={3}
                    data-active={active?.week === w && active.row === r ? '' : undefined}
                  />
                ) : null,
              ),
            )}
          </svg>
          {activeDay && tip && active && (
            <Tooltip
              x={LEFT + active.week * STEP + CELL / 2}
              width={width}
              title={dateFormat.format(new Date(`${activeDay.date}T00:00:00`))}
              rows={tip.rows}
            />
          )}
        </div>
      </div>

      <div className="heat-legend" aria-hidden="true">
        Less
        {[0, 1, 2, 3, 4].map((l) => (
          <span key={l} className={`heat-swatch heat-${l}`} />
        ))}
        More
      </div>

      <button type="button" className="btn small ghost" onClick={() => setShowTable((v) => !v)} aria-expanded={showTable}>
        {showTable ? 'Hide table' : 'Show as table'}
      </button>
      {showTable && (
        <ChartTable
          caption="Active days per month"
          columns={['Month', 'Active days']}
          rows={monthlyActiveDays(cells).map((m) => [m.label, String(m.activeDays)])}
        />
      )}
    </div>
  )
}
