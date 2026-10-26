import { useState } from 'react'
import { ChartTable } from './ChartTable'
import { formatTick, linear, niceTicks } from './scale'
import { Tooltip } from './Tooltip'
import { useWidth } from './useWidth'

export interface Bar {
  key: string
  /** Short axis label. */
  label: string
  /** Longer label for the tooltip and table. */
  title: string
  value: number
}

interface Props {
  bars: Bar[]
  ariaLabel: string
  unit: string
  /** CSS colour, normally a --series-N token. */
  color: string
  height?: number
}

const M = { top: 12, right: 8, bottom: 28, left: 44 }
const MAX_BAR = 24
const RADIUS = 4

/** A column with its data end rounded and its baseline end square. */
function columnPath(x: number, y: number, w: number, h: number): string {
  const r = Math.min(RADIUS, w / 2, h)
  return `M${x},${y + h}V${y + r}Q${x},${y} ${x + r},${y}H${x + w - r}Q${x + w},${y} ${x + w},${y + r}V${y + h}Z`
}

export function BarChart({ bars, ariaLabel, unit, color, height = 220 }: Props) {
  const [ref, width] = useWidth<HTMLDivElement>()
  const [active, setActive] = useState<number | null>(null)
  const [showTable, setShowTable] = useState(false)

  const yAxis = niceTicks(0, Math.max(...bars.map((b) => b.value), 1), 4)
  const innerW = Math.max(width - M.left - M.right, 10)
  const innerH = height - M.top - M.bottom
  const sy = linear([yAxis.min, yAxis.max], [M.top + innerH, M.top])
  const band = innerW / bars.length
  const barW = Math.min(MAX_BAR, band * 0.6)
  // Thin the axis labels so they never collide on narrow screens.
  const labelEvery = Math.ceil(36 / band)

  return (
    <div className="chart">
      <div className="chart-plot" ref={ref}>
        <svg width={width} height={height} role="img" aria-label={ariaLabel}>
          {yAxis.ticks.map((t) => (
            <g key={t}>
              <line className={t === 0 ? 'chart-baseline' : 'chart-grid'} x1={M.left} x2={M.left + innerW} y1={sy(t)} y2={sy(t)} />
              <text className="chart-label" x={M.left - 8} y={sy(t)} textAnchor="end" dominantBaseline="middle">
                {formatTick(t)}
              </text>
            </g>
          ))}
          {bars.map((b, i) => {
            const cx = M.left + band * i + band / 2
            const h = M.top + innerH - sy(b.value)
            return (
              <g key={b.key}>
                {h > 0 && (
                  <path className="chart-bar" d={columnPath(cx - barW / 2, sy(b.value), barW, h)} fill={color} opacity={active === null || active === i ? 1 : 0.55} />
                )}
                {i % labelEvery === 0 && (
                  <text className="chart-label" x={cx} y={height - 8} textAnchor="middle">
                    {b.label}
                  </text>
                )}
                <rect
                  x={M.left + band * i}
                  y={M.top}
                  width={band}
                  height={innerH}
                  fill="transparent"
                  onPointerEnter={() => setActive(i)}
                  onPointerMove={() => setActive(i)}
                  onPointerLeave={() => setActive(null)}
                />
              </g>
            )
          })}
        </svg>
        {active !== null && (
          <Tooltip
            x={M.left + band * active + band / 2}
            width={width}
            title={bars[active].title}
            rows={[{ label: ariaLabel, value: `${formatTick(bars[active].value)} ${unit}`, color }]}
          />
        )}
      </div>
      <button type="button" className="btn small ghost" onClick={() => setShowTable((v) => !v)} aria-expanded={showTable}>
        {showTable ? 'Hide table' : 'Show as table'}
      </button>
      {showTable && (
        <ChartTable
          caption={ariaLabel}
          columns={['Period', `Value (${unit})`]}
          rows={bars.map((b) => [b.title, formatTick(b.value)])}
        />
      )}
    </div>
  )
}
