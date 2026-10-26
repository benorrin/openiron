import { useState, type FormEvent } from 'react'
import { useSaveSchedule, type Schedule } from '../api/consistency'
import { useTemplates } from '../api/hooks'

// Monday first, to match how weeks are laid out everywhere. The number is JavaScript's getDay().
const DAYS = [
  { day: 1, name: 'Monday' },
  { day: 2, name: 'Tuesday' },
  { day: 3, name: 'Wednesday' },
  { day: 4, name: 'Thursday' },
  { day: 5, name: 'Friday' },
  { day: 6, name: 'Saturday' },
  { day: 0, name: 'Sunday' },
]

interface Props {
  schedule: Schedule
  onSaved?: () => void
}

/** Which days of the week to train, optionally with the template planned for each, and a daily step goal. */
export function ScheduleEditor({ schedule, onSaved }: Props) {
  const { data: templates = [] } = useTemplates()
  const save = useSaveSchedule()
  const [chosen, setChosen] = useState<Map<number, string | null>>(
    () => new Map(schedule.days.map((d) => [d.dayOfWeek, d.templateId])),
  )
  const [stepGoal, setStepGoal] = useState(String(schedule.stepGoal))

  function toggle(day: number, on: boolean) {
    setChosen((current) => {
      const next = new Map(current)
      if (on) next.set(day, null)
      else next.delete(day)
      return next
    })
  }

  async function submit(e: FormEvent) {
    e.preventDefault()
    try {
      await save.mutateAsync({
        days: [...chosen.entries()].map(([dayOfWeek, templateId]) => ({ dayOfWeek, templateId })),
        stepGoal: Number(stepGoal),
      })
      onSaved?.()
    } catch {
      // Shown from save.error.
    }
  }

  return (
    <form className="stack" onSubmit={submit}>
      <ul className="schedule-days">
        {DAYS.map(({ day, name }) => {
          const on = chosen.has(day)
          return (
            <li key={day}>
              <label className="switch-row" style={{ alignItems: 'center' }}>
                <input type="checkbox" className="switch" role="switch" checked={on} onChange={(e) => toggle(day, e.target.checked)} />
                <strong style={{ minWidth: 96 }}>{name}</strong>
              </label>
              {on && (
                <select
                  aria-label={`Template for ${name}`}
                  value={chosen.get(day) ?? ''}
                  onChange={(e) => setChosen((c) => new Map(c).set(day, e.target.value || null))}
                >
                  <option value="">Any workout or activity</option>
                  {templates.map((t) => (
                    <option key={t.id} value={t.id}>
                      {t.name}
                    </option>
                  ))}
                </select>
              )}
            </li>
          )
        })}
      </ul>

      <label className="field" style={{ maxWidth: 240 }}>
        Daily step goal
        <input type="number" inputMode="numeric" min={1000} max={100000} step={500} value={stepGoal} onChange={(e) => setStepGoal(e.target.value)} required />
      </label>
      <p className="muted">
        Any logged workout or activity completes a scheduled day, and so does reaching your step goal. Adherence is
        measured from the day you last changed which days you train on.
      </p>

      {save.error && <p className="error" role="alert">{save.error.message}</p>}
      <div>
        <button className="btn primary" disabled={save.isPending}>
          Save schedule
        </button>
      </div>
    </form>
  )
}
