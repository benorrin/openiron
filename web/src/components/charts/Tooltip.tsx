import type { ReactNode } from 'react'

export interface TooltipRow {
  label: string
  value: string
  /** CSS colour for the line key; omit for rows that aren't a series. */
  color?: string
}

interface Props {
  /** Anchor x within the chart container, in px. */
  x: number
  width: number
  title: string
  rows: TooltipRow[]
}

/** Values lead (strong), labels follow (secondary); rows are keyed by a short line, not a box. */
export function Tooltip({ x, width, title, rows }: Props): ReactNode {
  const flip = x > width / 2
  return (
    <div
      className="chart-tooltip"
      role="status"
      style={{ left: x, transform: `translateX(${flip ? 'calc(-100% - 12px)' : '12px'})` }}
    >
      <div className="chart-tooltip-title">{title}</div>
      {rows.map((r) => (
        <div key={r.label} className="chart-tooltip-row">
          {r.color && <span className="chart-key" style={{ background: r.color }} />}
          <strong>{r.value}</strong>
          <span>{r.label}</span>
        </div>
      ))}
    </div>
  )
}
