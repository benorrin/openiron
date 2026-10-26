import type { Macros } from '../api/nutrition'
import { macroShares } from '../lib/nutrition'

const PARTS = [
  { key: 'protein', label: 'Protein', color: 'var(--series-1)' },
  { key: 'carbs', label: 'Carbs', color: 'var(--series-2)' },
  { key: 'fat', label: 'Fat', color: 'var(--series-3)' },
] as const

/** Where the calories come from. The legend carries the exact numbers, so the colours never have to. */
export function MacroBar({ macros }: { macros: Macros }) {
  const shares = macroShares(macros)
  const grams = { protein: macros.proteinG, carbs: macros.carbsG, fat: macros.fatG }

  return (
    <div className="stack" style={{ gap: 10 }}>
      <div className="macro-bar" role="img" aria-label={PARTS.map((p) => `${p.label} ${grams[p.key]} grams`).join(', ')}>
        {PARTS.map((p) => (
          <span key={p.key} style={{ flexGrow: shares[p.key], background: p.color }} />
        ))}
      </div>
      <ul className="macro-legend">
        {PARTS.map((p) => (
          <li key={p.key}>
            <span className="chart-key" style={{ background: p.color }} />
            <strong>{grams[p.key]} g</strong>
            <span className="muted">
              {p.label} · {Math.round(shares[p.key] * 100)}%
            </span>
          </li>
        ))}
      </ul>
    </div>
  )
}
