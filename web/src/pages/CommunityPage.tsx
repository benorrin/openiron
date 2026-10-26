import { Link } from 'react-router-dom'
import { useCommunityMembers, useCommunitySettings, useUpdateCommunitySettings } from '../api/hooks'
import { formatDate, formatVolume } from '../lib/format'

export function CommunityPage() {
  const { data: settings } = useCommunitySettings()
  const update = useUpdateCommunitySettings()
  const { data: members, isLoading, error } = useCommunityMembers()

  return (
    <div className="page">
      <h1>Community</h1>

      <section className="card stack">
        <label className="row" style={{ flexWrap: 'nowrap', alignItems: 'flex-start' }}>
          <input
            type="checkbox"
            className="check"
            checked={settings?.shareActivity ?? false}
            disabled={!settings || update.isPending}
            onChange={(e) => update.mutate(e.target.checked)}
          />
          <span>
            <strong>Share my training with others on this server</strong>
            <span className="muted" style={{ display: 'block' }}>
              Other members can see your workout counts, volume, main lifts and recent workout names. Your body weight and
              notes are never shared. This is off by default and you can turn it off at any time.
            </span>
          </span>
        </label>
        {update.error && <p className="error" role="alert">{update.error.message}</p>}
      </section>

      {isLoading && <p className="empty">Loading…</p>}
      {error && <p className="error">{error.message}</p>}
      {members?.length === 0 && (
        <div className="card empty">
          <p>Nobody is sharing yet. Turn sharing on above to appear here.</p>
        </div>
      )}

      {members && members.length > 0 && (
        <ul className="card list">
          {members.map((m) => (
            <li key={m.username}>
              <Link to={`/community/${encodeURIComponent(m.username)}`} className="list-link">
                <div>
                  <strong>{m.username}</strong> {m.isYou && <span className="pill">You</span>}
                  <p className="muted">
                    {m.lastWorkoutAt ? `Last trained ${formatDate(m.lastWorkoutAt)}` : 'No workouts yet'}
                  </p>
                </div>
                <div style={{ textAlign: 'right' }}>
                  <strong>{m.workoutsLast30Days}</strong>
                  <span className="muted"> workouts in 30 days</span>
                  <p className="muted">{formatVolume(m.volumeLast30DaysKg)} kg lifted</p>
                </div>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
