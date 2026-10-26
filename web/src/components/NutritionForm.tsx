import { useState, type FormEvent } from 'react'
import {
  useSaveNutritionSettings,
  type ActivityLevel,
  type GoalType,
  type NutritionSettings,
  type Sex,
} from '../api/nutrition'
import { ACTIVITY_OPTIONS, DEFAULT_RATE, RATE_OPTIONS } from '../lib/nutrition'

const GOALS: { value: GoalType; label: string }[] = [
  { value: 'lose', label: 'Lose weight' },
  { value: 'maintain', label: 'Maintain' },
  { value: 'gain', label: 'Gain weight' },
]

const RATE_LABELS: Record<number, string> = { 0.1: 'slow', 0.25: 'gentle', 0.5: 'moderate', 0.75: 'fast', 1: 'aggressive' }

interface Props {
  initial: NutritionSettings | null
  submitLabel: string
  onSaved?: () => void
}

/** Profile and goal. The profile only feeds the calorie formula; none of it is shown to anyone else. */
export function NutritionForm({ initial, submitLabel, onSaved }: Props) {
  const save = useSaveNutritionSettings()
  const [sex, setSex] = useState<Sex>(initial?.sex ?? 'male')
  const [dob, setDob] = useState(initial?.dateOfBirth ?? '')
  const [height, setHeight] = useState(initial ? String(initial.heightCm) : '')
  const [activity, setActivity] = useState<ActivityLevel>(initial?.activity ?? 'moderate')
  const [goal, setGoal] = useState<GoalType>(initial?.goal ?? 'lose')
  const [target, setTarget] = useState(initial?.targetWeightKg ? String(initial.targetWeightKg) : '')
  const [rate, setRate] = useState(initial && initial.goal !== 'maintain' ? initial.weeklyRateKg : DEFAULT_RATE.lose)
  const [maxDob] = useState(() => {
    const d = new Date()
    d.setFullYear(d.getFullYear() - 18)
    return d.toISOString().slice(0, 10)
  })

  function chooseGoal(next: GoalType) {
    setGoal(next)
    // Keep the pace valid for the new direction: a 1 kg gain a week isn't on offer.
    if (next !== 'maintain' && !RATE_OPTIONS[next].includes(rate)) setRate(DEFAULT_RATE[next])
  }

  async function submit(e: FormEvent) {
    e.preventDefault()
    try {
      await save.mutateAsync({
        sex,
        dateOfBirth: dob,
        heightCm: Number(height),
        activity,
        goal,
        targetWeightKg: goal === 'maintain' ? null : Number(target),
        weeklyRateKg: goal === 'maintain' ? 0 : rate,
      })
      onSaved?.()
    } catch {
      // Shown from save.error.
    }
  }

  return (
    <form className="stack" onSubmit={submit}>
      <div className="row">
        <label className="field grow">
          Sex
          <select value={sex} onChange={(e) => setSex(e.target.value as Sex)}>
            <option value="male">Male</option>
            <option value="female">Female</option>
            <option value="unspecified">Prefer not to say</option>
          </select>
        </label>
        <label className="field grow">
          Date of birth
          <input type="date" value={dob} max={maxDob} onChange={(e) => setDob(e.target.value)} required />
        </label>
        <label className="field grow">
          Height (cm)
          <input type="number" inputMode="decimal" min={120} max={230} value={height} onChange={(e) => setHeight(e.target.value)} required />
        </label>
      </div>
      <p className="muted">Used only in the calorie formula. It is never shown to other people.</p>

      <label className="field">
        Activity level
        <select value={activity} onChange={(e) => setActivity(e.target.value as ActivityLevel)}>
          {ACTIVITY_OPTIONS.map((o) => (
            <option key={o.value} value={o.value}>
              {o.label}: {o.hint}
            </option>
          ))}
        </select>
        <span className="muted">Count your training and daily movement together. If unsure, pick the lower one: the check-ins correct it over time.</span>
      </label>

      <div className="field">
        Goal
        <div className="tabs" role="group" aria-label="Goal">
          {GOALS.map((g) => (
            <button key={g.value} type="button" aria-pressed={goal === g.value} onClick={() => chooseGoal(g.value)}>
              {g.label}
            </button>
          ))}
        </div>
      </div>

      {goal !== 'maintain' && (
        <div className="row">
          <label className="field grow">
            Target weight (kg)
            <input type="number" inputMode="decimal" step="0.1" min={30} max={300} value={target} onChange={(e) => setTarget(e.target.value)} required />
          </label>
          <label className="field grow">
            Pace
            <select value={rate} onChange={(e) => setRate(Number(e.target.value))}>
              {RATE_OPTIONS[goal].map((r) => (
                <option key={r} value={r}>
                  {r} kg a week ({RATE_LABELS[r]})
                </option>
              ))}
            </select>
          </label>
        </div>
      )}

      {save.error && <p className="error" role="alert">{save.error.message}</p>}
      <div>
        <button className="btn primary" disabled={save.isPending}>
          {submitLabel}
        </button>
      </div>
      <p className="muted">
        This is a guide, not medical advice. Check with a doctor before a large change to how you eat, especially if you
        have a medical condition. Calorie targets are only calculated for adults.
      </p>
    </form>
  )
}
