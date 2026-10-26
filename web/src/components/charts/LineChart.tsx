import { useMemo, useState, type KeyboardEvent, type PointerEvent } from 'react'
import { ChartTable } from './ChartTable'
import { evenTicks, formatTick, linear, nearestIndex, niceTicks } from './scale'
import { Tooltip } from './Tooltip'
import { useWidth } from './useWidth'

export interface LineSeries {
  id: string
  label: string
  /** CSS colour, normally a --series-N token. */
  color: string
  points: { x: number; y: number }[]
}

interface Props {
  series: LineSeries[]
  ariaLabel: string
  unit: string
  formatX: (x: number) => string
  height?: number
  /** Wash the area under the first series (single-series charts). */
  area?: boolean
}

const M = { top: 12, right: 16, bottom: 28, left: 44 }
const DAY = 86_400_000

export function LineChart({ series, ariaLabel, unit, formatX, height = 240, area = false }: Props) {
  const [ref, width] = useWidth<HTMLDivElement>()
  const [active, setActive] = useState<number | null>(null)
  const [showTable, setShowTable] = useState(false)

  const model = useMemo(() => {
    const xs = [...new Set(series.flatMap((s) => s.points.map((p) => p.x)))].sort((a, b) => a - b)
    const ys = series.flatMap((s) => s.points.map((p) => p.y))
    return { xs, y: niceTicks(Math.min(...ys), Math.max(...ys), 4) }
  }, [series])

  const { xs, y: yAxis } = model
  if (xs.length === 0) return null

  const innerW = Math.max(width - M.left - M.right, 10)
  const innerH = height - M.top - M.bottom
  // A lone session gets a window around it rather than sitting on the axis.
  const [x0, x1] = xs.length === 1 ? [xs[0] - 3 * DAY, xs[0] + 3 * DAY] : [xs[0], xs[xs.length - 1]]
  const sx = linear([x0, x1], [M.left, M.left + innerW])
  const sy = linear([yAxis.min, yAxis.max], [M.top + innerH, M.top])
  const xTicks = evenTicks(x0, x1, Math.max(2, Math.min(5, Math.floor(innerW / 90))))

  const pathFor = (s: LineSeries) => s.points.map((p, i) => `${i ? 'L' : 'M'}${sx(p.x)},${sy(p.y)}`).join('')
  const activeX = active !== null ? xs[active] : null

  function onMove(e: PointerEvent<SVGRectElement>) {
    const rect = e.currentTarget.getBoundingClientRect()
    const target = x0 + ((e.clientX - rect.left) / rect.width) * (x1 - x0)
    setActive(nearestIndex(xs, target))
  }

  function onKey(e: KeyboardEvent<SVGSVGElement>) {
    if (e.key !== 'ArrowLeft' && e.key !== 'ArrowRight') return
    e.preventDefault()
    const step = e.key === 'ArrowLeft' ? -1 : 1
    setActive((a) => Math.min(Math.max((a ?? xs.length - 1) + step, 0), xs.length - 1))
  }

  const tooltipRows =
    activeX === null
      ? []
      : series.flatMap((s) => {
          const p = s.points.find((pt) => pt.x === activeX)
          return p ? [{ label: s.label, value: `${formatTick(p.y)} ${unit}`, color: s.color }] : []
        })

  return (
    <div className="chart">
      {series.length > 1 && (
        <ul className="chart-legend">
          {series.map((s) => (
            <li key={s.id}>
              <span className="chart-key" style={{ background: s.color }} />
              {s.label}
            </li>
          ))}
        </ul>
      )}
      <div className="chart-plot" ref={ref}>
        <svg
          width={width}
          height={height}
          role="img"
          aria-label={ariaLabel}
          tabIndex={0}
          onKeyDown={onKey}
          onFocus={() => setActive((a) => a ?? xs.length - 1)}
          onBlur={() => setActive(null)}
        >
          {yAxis.ticks.map((t) => (
            <g key={t}>
              <line className="chart-grid" x1={M.left} x2={M.left + innerW} y1={sy(t)} y2={sy(t)} />
              <text className="chart-label" x={M.left - 8} y={sy(t)} textAnchor="end" dominantBaseline="middle">
                {formatTick(t)}
              </text>
            </g>
          ))}
          {xTicks.map((t, i) => (
            <text
              key={t}
              className="chart-label"
              x={sx(t)}
              y={height - 8}
              textAnchor={i === 0 && xTicks.length > 1 ? 'start' : i === xTicks.length - 1 && xTicks.length > 1 ? 'end' : 'middle'}
            >
              {formatX(t)}
            </text>
          ))}

          {area && series[0] && series[0].points.length > 1 && (
            <path
              className="chart-area"
              d={`${pathFor(series[0])}L${sx(series[0].points[series[0].points.length - 1].x)},${M.top + innerH}L${sx(series[0].points[0].x)},${M.top + innerH}Z`}
              fill={series[0].color}
              opacity={0.1}
            />
          )}
          {series.map((s) => (
            <path key={s.id} className="chart-line" pathLength={1} d={pathFor(s)} fill="none" stroke={s.color} strokeWidth={2} strokeLinejoin="round" strokeLinecap="round" />
          ))}
          {series.map((s) =>
            s.points.map((p) => (
              <circle
                key={`${s.id}-${p.x}`}
                cx={sx(p.x)}
                cy={sy(p.y)}
                r={p.x === activeX ? 5 : 4}
                fill={s.color}
                className="chart-dot"
              />
            )),
          )}

          {activeX !== null && <line className="chart-crosshair" x1={sx(activeX)} x2={sx(activeX)} y1={M.top} y2={M.top + innerH} />}
          <rect
            x={M.left}
            y={M.top}
            width={innerW}
            height={innerH}
            fill="transparent"
            onPointerMove={onMove}
            onPointerDown={onMove}
            onPointerLeave={() => setActive(null)}
          />
        </svg>
        {activeX !== null && <Tooltip x={sx(activeX)} width={width} title={formatX(activeX)} rows={tooltipRows} />}
      </div>
      <button type="button" className="btn small ghost" onClick={() => setShowTable((v) => !v)} aria-expanded={showTable}>
        {showTable ? 'Hide table' : 'Show as table'}
      </button>
      {showTable && (
        <ChartTable
          caption={ariaLabel}
          columns={['Date', ...series.map((s) => `${s.label} (${unit})`)]}
          rows={xs.map((x) => [
            formatX(x),
            ...series.map((s) => {
              const p = s.points.find((pt) => pt.x === x)
              return p ? formatTick(p.y) : '–'
            }),
          ])}
        />
      )}
    </div>
  )
}
