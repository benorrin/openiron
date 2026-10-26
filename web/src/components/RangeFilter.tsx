import type { Range } from '../lib/range'
import { RANGES } from '../lib/range'

interface Props {
  value: Range
  onChange: (range: Range) => void
}

/** One row of presets that scopes every chart and figure below it. */
export function RangeFilter({ value, onChange }: Props) {
  return (
    <div className="tabs range-filter" role="group" aria-label="Date range">
      {RANGES.map((r) => (
        <button key={r.label} type="button" aria-pressed={r.label === value.label} onClick={() => onChange(r)}>
          {r.label}
        </button>
      ))}
    </div>
  )
}
