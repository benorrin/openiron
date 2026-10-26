import { useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { useDeleteWorkout, useSaveWorkout, useWorkout, useWorkoutDraft } from '../api/hooks'
import type { Exercise, WorkoutExercise, WorkoutInput } from '../api/types'
import { ExercisePicker } from '../components/ExercisePicker'
import { formatKg, formatVolume, fromLocalInput, toLocalInput } from '../lib/format'

interface SetRow {
  reps: string
  weightKg: string
}

interface ExerciseRow {
  exerciseId: string
  name: string
  sets: SetRow[]
}

interface Initial {
  name: string
  /** Local datetime-input value; null means "now", resolved when the form first mounts. */
  performedAt: string | null
  durationMinutes: string
  notes: string
  templateId: string | null
  exercises: ExerciseRow[]
}

const toRows = (exercises: WorkoutExercise[]): ExerciseRow[] =>
  exercises.map((e) => ({
    exerciseId: e.exerciseId,
    name: e.exerciseName,
    sets: e.sets.map((s) => ({ reps: String(s.reps), weightKg: formatKg(s.weightKg) })),
  }))

export function WorkoutEditPage() {
  const { id } = useParams()
  const [search] = useSearchParams()
  const templateId = id ? null : search.get('template')

  const existing = useWorkout(id)
  const draft = useWorkoutDraft(templateId)

  if (id) {
    if (existing.isLoading) return <p className="empty">Loading…</p>
    if (!existing.data) return <p className="error page">{existing.error?.message ?? 'Workout not found.'}</p>
    const w = existing.data
    return (
      <WorkoutForm
        key={w.id}
        id={w.id}
        initial={{
          name: w.name,
          performedAt: toLocalInput(w.performedAt),
          durationMinutes: w.durationMinutes ? String(w.durationMinutes) : '',
          notes: w.notes ?? '',
          templateId: w.templateId,
          exercises: toRows(w.exercises),
        }}
      />
    )
  }

  if (templateId) {
    if (draft.isLoading) return <p className="empty">Loading…</p>
    if (!draft.data) return <p className="error page">{draft.error?.message ?? 'Template not found.'}</p>
    return (
      <WorkoutForm
        key={templateId}
        id={undefined}
        initial={{
          name: draft.data.name,
          performedAt: null,
          durationMinutes: '',
          notes: '',
          templateId: draft.data.templateId,
          exercises: toRows(draft.data.exercises),
        }}
      />
    )
  }

  return (
    <WorkoutForm
      key="empty"
      id={undefined}
      initial={{
        name: 'Workout',
        performedAt: null,
        durationMinutes: '',
        notes: '',
        templateId: null,
        exercises: [],
      }}
    />
  )
}

function WorkoutForm({ id, initial }: { id: string | undefined; initial: Initial }) {
  const navigate = useNavigate()
  const save = useSaveWorkout(id)
  const remove = useDeleteWorkout()

  const [name, setName] = useState(initial.name)
  const [performedAt, setPerformedAt] = useState(() => initial.performedAt ?? toLocalInput(new Date().toISOString()))
  const [duration, setDuration] = useState(initial.durationMinutes)
  const [notes, setNotes] = useState(initial.notes)
  const [exercises, setExercises] = useState(initial.exercises)
  const [formError, setFormError] = useState<string | null>(null)

  const updateExercise = (i: number, fn: (e: ExerciseRow) => ExerciseRow) =>
    setExercises((xs) => xs.map((x, j) => (j === i ? fn(x) : x)))

  const updateSet = (i: number, k: number, patch: Partial<SetRow>) =>
    updateExercise(i, (e) => ({ ...e, sets: e.sets.map((s, m) => (m === k ? { ...s, ...patch } : s)) }))

  const addSet = (i: number) =>
    updateExercise(i, (e) => ({ ...e, sets: [...e.sets, { ...(e.sets[e.sets.length - 1] ?? { reps: '', weightKg: '' }) }] }))

  const removeSet = (i: number, k: number) =>
    updateExercise(i, (e) => ({ ...e, sets: e.sets.filter((_, m) => m !== k) }))

  const addExercise = (e: Exercise) =>
    setExercises((xs) => [...xs, { exerciseId: e.id, name: e.name, sets: [{ reps: '', weightKg: '' }] }])

  const volume = exercises.reduce(
    (sum, e) => sum + e.sets.reduce((s, set) => s + (Number(set.reps) || 0) * (Number(set.weightKg) || 0), 0),
    0,
  )

  async function submit(ev: FormEvent) {
    ev.preventDefault()
    setFormError(null)

    // Exercises with no sets are treated as skipped rather than rejected.
    const logged = exercises.filter((e) => e.sets.length > 0)
    if (logged.length === 0) return setFormError('Log at least one set.')
    for (const e of logged)
      for (const s of e.sets) {
        if (!(Number(s.reps) >= 1) || s.weightKg === '' || !(Number(s.weightKg) >= 0))
          return setFormError(`Enter reps and weight for every set of ${e.name} (use 0 kg for bodyweight).`)
      }

    const input: WorkoutInput = {
      name: name.trim(),
      performedAt: fromLocalInput(performedAt),
      durationMinutes: duration ? Number(duration) : null,
      notes: notes.trim() || null,
      templateId: initial.templateId,
      exercises: logged.map((e) => ({
        exerciseId: e.exerciseId,
        sets: e.sets.map((s) => ({ reps: Math.round(Number(s.reps)), weightKg: Number(s.weightKg) })),
      })),
    }

    await save.mutateAsync(input)
    navigate('/workouts')
  }

  return (
    <form className="page" onSubmit={submit}>
      <div className="page-header">
        <h1>{id ? 'Edit workout' : 'Log workout'}</h1>
        <Link to="/workouts" className="btn ghost">
          Cancel
        </Link>
      </div>

      <div className="card stack">
        <label className="field">
          Name
          <input value={name} onChange={(e) => setName(e.target.value)} maxLength={100} required />
        </label>
        <div className="row">
          <label className="field grow">
            When
            <input type="datetime-local" value={performedAt} onChange={(e) => setPerformedAt(e.target.value)} required />
          </label>
          <label className="field grow">
            Duration (min)
            <input type="number" inputMode="numeric" min={1} max={1440} value={duration} onChange={(e) => setDuration(e.target.value)} />
          </label>
        </div>
      </div>

      {exercises.map((e, i) => (
        <section key={`${e.exerciseId}-${i}`} className="card stack">
          <div className="row">
            <h2 className="grow">{e.name}</h2>
            <button type="button" className="btn small ghost danger" onClick={() => setExercises((xs) => xs.filter((_, j) => j !== i))}>
              Remove
            </button>
          </div>
          {e.sets.map((s, k) => (
            <div key={k} className="row set-row">
              <span className="muted set-num">{k + 1}</span>
              <label className="field grow">
                <span className="sr-only">Set {k + 1} weight in kg</span>
                <input
                  type="number"
                  inputMode="decimal"
                  step="0.5"
                  min={0}
                  max={1000}
                  placeholder="kg"
                  aria-label={`${e.name} set ${k + 1} weight in kg`}
                  value={s.weightKg}
                  onChange={(ev) => updateSet(i, k, { weightKg: ev.target.value })}
                />
              </label>
              <span className="muted">kg ×</span>
              <label className="field grow">
                <input
                  type="number"
                  inputMode="numeric"
                  min={1}
                  max={500}
                  placeholder="reps"
                  aria-label={`${e.name} set ${k + 1} reps`}
                  value={s.reps}
                  onChange={(ev) => updateSet(i, k, { reps: ev.target.value })}
                />
              </label>
              <button type="button" className="btn small ghost" aria-label={`Remove set ${k + 1}`} onClick={() => removeSet(i, k)}>
                ✕
              </button>
            </div>
          ))}
          <div>
            <button type="button" className="btn small" onClick={() => addSet(i)}>
              + Add set
            </button>
          </div>
        </section>
      ))}

      <div className="card stack">
        <ExercisePicker onPick={addExercise} />
        <label className="field">
          Notes
          <textarea value={notes} onChange={(e) => setNotes(e.target.value)} rows={2} maxLength={2000} />
        </label>
      </div>

      {formError && <p className="error" role="alert">{formError}</p>}
      {save.error && <p className="error" role="alert">{save.error.message}</p>}

      <div className="row">
        <button className="btn primary grow" disabled={save.isPending}>
          Save workout · {formatVolume(volume)} kg
        </button>
        {id && (
          <button
            type="button"
            className="btn danger"
            disabled={remove.isPending}
            onClick={async () => {
              if (!confirm('Delete this workout?')) return
              await remove.mutateAsync(id)
              navigate('/workouts')
            }}
          >
            Delete
          </button>
        )}
      </div>
    </form>
  )
}
