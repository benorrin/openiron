import { useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useSaveTemplate, useTemplate } from '../api/hooks'
import type { Exercise, Template, TemplateInput } from '../api/types'
import { ExercisePicker } from '../components/ExercisePicker'
import { PageSkeleton } from '../components/Skeleton'

interface Row {
  exerciseId: string
  name: string
  targetSets: number
  targetReps: number
}

export function TemplateEditPage() {
  const { id } = useParams()
  const { data: existing, isLoading, error } = useTemplate(id)

  if (id && isLoading) return <PageSkeleton />
  if (id && !existing) return <p className="error page">{error?.message ?? 'Template not found.'}</p>
  return <TemplateForm key={id ?? 'new'} id={id} existing={existing} />
}

function TemplateForm({ id, existing }: { id: string | undefined; existing: Template | undefined }) {
  const navigate = useNavigate()
  const save = useSaveTemplate(id)

  const [name, setName] = useState(existing?.name ?? '')
  const [notes, setNotes] = useState(existing?.notes ?? '')
  const [rows, setRows] = useState<Row[]>(
    () =>
      existing?.exercises.map((e) => ({
        exerciseId: e.exerciseId,
        name: e.exerciseName,
        targetSets: e.targetSets,
        targetReps: e.targetReps,
      })) ?? [],
  )

  const update = (i: number, patch: Partial<Row>) =>
    setRows((rs) => rs.map((r, j) => (j === i ? { ...r, ...patch } : r)))

  const move = (i: number, by: -1 | 1) =>
    setRows((rs) => {
      const j = i + by
      if (j < 0 || j >= rs.length) return rs
      const next = [...rs]
      ;[next[i], next[j]] = [next[j], next[i]]
      return next
    })

  const add = (e: Exercise) =>
    setRows((rs) => [...rs, { exerciseId: e.id, name: e.name, targetSets: 3, targetReps: 8 }])

  async function submit(e: FormEvent) {
    e.preventDefault()
    const input: TemplateInput = {
      name: name.trim(),
      notes: notes.trim() || null,
      exercises: rows.map(({ exerciseId, targetSets, targetReps }) => ({ exerciseId, targetSets, targetReps })),
    }
    await save.mutateAsync(input)
    navigate('/templates')
  }

  return (
    <form className="page" onSubmit={submit}>
      <div className="page-header">
        <h1>{id ? 'Edit template' : 'New template'}</h1>
        <Link to="/templates" className="btn ghost">
          Cancel
        </Link>
      </div>

      <div className="card stack">
        <label className="field">
          Name
          <input value={name} onChange={(e) => setName(e.target.value)} placeholder="Leg day" maxLength={100} required />
        </label>
        <label className="field">
          Notes
          <textarea value={notes} onChange={(e) => setNotes(e.target.value)} rows={2} maxLength={2000} />
        </label>
      </div>

      <div className="card stack">
        <h2>Exercises</h2>
        {rows.length === 0 && <p className="muted">Add at least one exercise.</p>}
        {rows.map((r, i) => (
          <div key={`${r.exerciseId}-${i}`} className="stack" style={{ borderTop: i ? '1px solid var(--grid)' : 0, paddingTop: i ? 12 : 0 }}>
            <div className="row">
              <strong className="grow">{r.name}</strong>
              <button type="button" className="btn small ghost" onClick={() => move(i, -1)} disabled={i === 0} aria-label={`Move ${r.name} up`}>
                ↑
              </button>
              <button type="button" className="btn small ghost" onClick={() => move(i, 1)} disabled={i === rows.length - 1} aria-label={`Move ${r.name} down`}>
                ↓
              </button>
              <button type="button" className="btn small ghost danger" onClick={() => setRows((rs) => rs.filter((_, j) => j !== i))}>
                Remove
              </button>
            </div>
            <div className="row">
              <label className="field grow">
                Sets
                <input type="number" inputMode="numeric" min={1} max={20} value={r.targetSets} onChange={(e) => update(i, { targetSets: Number(e.target.value) })} required />
              </label>
              <label className="field grow">
                Reps
                <input type="number" inputMode="numeric" min={1} max={100} value={r.targetReps} onChange={(e) => update(i, { targetReps: Number(e.target.value) })} required />
              </label>
            </div>
          </div>
        ))}
        <ExercisePicker onPick={add} />
      </div>

      {save.error && <p className="error" role="alert">{save.error.message}</p>}
      <button className="btn primary" disabled={save.isPending || rows.length === 0}>
        Save template
      </button>
    </form>
  )
}
