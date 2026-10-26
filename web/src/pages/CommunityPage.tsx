import { Link } from 'react-router-dom'
import { useCommunityMembers, useCommunitySettings } from '../api/hooks'
import { formatDate, formatVolume } from '../lib/format'
import { PageSkeleton } from '../components/Skeleton'
import { EmptyState } from '../components/EmptyState'

export function CommunityPage() {
  const { data: settings } = useCommunitySettings()
  const { data: members, isLoading, error } = useCommunityMembers()

  return (
    <div className="page">
      <h1>Community</h1>

      <p className="muted">
        {settings?.shareActivity
          ? "You're sharing your training with everyone on this server. "
          : "You're not sharing your training, so you don't appear here. "}
        <Link to="/settings">Change this in Settings</Link>.
      </p>

      {isLoading && <PageSkeleton />}
      {error && <p className="error">{error.message}</p>}
      {members?.length === 0 && (
        <EmptyState icon="users" title="Nobody is sharing yet">
          Turn sharing on in Settings to appear here.
        </EmptyState>
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
