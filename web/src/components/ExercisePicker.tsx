import { useState, type FormEvent } from 'react'
import { useCreateExercise, useExercises } from '../api/hooks'
import type { Exercise } from '../api/types'
import { groupByMuscle } from '../lib/exercises'

const MUSCLE_GROUPS = ['Legs', 'Back', 'Chest', 'Shoulders', 'Arms', 'Core', 'Other']

interface Props {
  onPick: (exercise: Exercise) => void
  label?: string
}

/** Picks from the exercise library, with an inline form for adding a custom exercise. */
export function ExercisePicker({ onPick, label = 'Add exercise' }: Props) {
  const { data: exercises = [] } = useExercises()
  const create = useCreateExercise()
  const [creating, setCreating] = useState(false)
  const [name, setName] = useState('')
  const [muscleGroup, setMuscleGroup] = useState(MUSCLE_GROUPS[0])

  async function submit(e: FormEvent) {
    e.preventDefault()
    const exercise = await create.mutateAsync({ name: name.trim(), muscleGroup })
    setName('')
    setCreating(false)
    onPick(exercise)
  }

  return (
    <div className="stack">
      <div className="row">
        <select
          className="grow"
          aria-label={label}
          value=""
          onChange={(e) => {
            const picked = exercises.find((x) => x.id === e.target.value)
            if (picked) onPick(picked)
          }}
        >
          <option value="">{label}…</option>
          {groupByMuscle(exercises).map(([group, items]) => (
            <optgroup key={group} label={group}>
              {items.map((x) => (
                <option key={x.id} value={x.id}>
                  {x.name}
                </option>
              ))}
            </optgroup>
          ))}
        </select>
        <button type="button" className="btn" onClick={() => setCreating((v) => !v)}>
          {creating ? 'Cancel' : 'New exercise'}
        </button>
      </div>
      {creating && (
        <form className="row" onSubmit={submit}>
          <input
            className="grow"
            placeholder="Exercise name"
            aria-label="Exercise name"
            value={name}
            onChange={(e) => setName(e.target.value)}
            maxLength={100}
            required
          />
          <select aria-label="Muscle group" value={muscleGroup} onChange={(e) => setMuscleGroup(e.target.value)} style={{ width: 'auto' }}>
            {MUSCLE_GROUPS.map((g) => (
              <option key={g}>{g}</option>
            ))}
          </select>
          <button className="btn primary" disabled={create.isPending}>
            Create
          </button>
          {create.error && <p className="error">{create.error.message}</p>}
        </form>
      )}
    </div>
  )
}
