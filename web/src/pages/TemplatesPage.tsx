import { Link } from 'react-router-dom'
import { useDeleteTemplate, useTemplates } from '../api/hooks'

export function TemplatesPage() {
  const { data: templates, isLoading, error } = useTemplates()
  const remove = useDeleteTemplate()

  return (
    <div className="page">
      <div className="page-header">
        <h1>Templates</h1>
        <Link to="/templates/new" className="btn primary">
          New template
        </Link>
      </div>

      {isLoading && <p className="empty">Loading…</p>}
      {error && <p className="error">{error.message}</p>}
      {templates?.length === 0 && (
        <div className="card empty">
          <p>No templates yet. Create one for a session you repeat, like leg day.</p>
        </div>
      )}

      {templates && templates.length > 0 && (
        <div className="stack">
          {templates.map((t) => (
            <section key={t.id} className="card stack">
              <div className="card-title" style={{ marginBottom: 0 }}>
                <h2>{t.name}</h2>
                <span className="muted">{t.exercises.length} exercises</span>
              </div>
              <ul className="muted" style={{ margin: 0, paddingLeft: 20 }}>
                {t.exercises.map((e) => (
                  <li key={e.exerciseId}>
                    {e.exerciseName} · {e.targetSets}×{e.targetReps}
                  </li>
                ))}
              </ul>
              <div className="row">
                <Link to={`/workouts/new?template=${t.id}`} className="btn primary">
                  Start workout
                </Link>
                <Link to={`/templates/${t.id}`} className="btn">
                  Edit
                </Link>
                <button
                  className="btn danger ghost"
                  onClick={() => {
                    if (confirm(`Delete "${t.name}"? Logged workouts are kept.`)) remove.mutate(t.id)
                  }}
                >
                  Delete
                </button>
              </div>
            </section>
          ))}
        </div>
      )}
    </div>
  )
}
