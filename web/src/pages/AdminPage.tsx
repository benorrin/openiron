import { useState, type FormEvent } from 'react'
import { Navigate } from 'react-router-dom'
import {
  useAdminOverview,
  useAdminUsers,
  useDeleteAdminUser,
  useResetUserPassword,
  useSetRegistrationOpen,
  useUpdateAdminUser,
} from '../api/hooks'
import type { AdminUser } from '../api/types'
import { useAuth } from '../auth/useAuth'
import { formatDate } from '../lib/format'
import { PageSkeleton } from '../components/Skeleton'

export function AdminPage() {
  const { user } = useAuth()
  // The API enforces this too; this just keeps non-admins off a page that can't work for them.
  if (!user?.isAdmin) return <Navigate to="/" replace />

  return (
    <div className="page">
      <h1>Admin</h1>
      <OverviewSection />
      <UsersSection />
    </div>
  )
}

function OverviewSection() {
  const { data: overview } = useAdminOverview()
  const setOpen = useSetRegistrationOpen()

  if (!overview) return <PageSkeleton />

  return (
    <>
      <div className="tiles">
        <div className="card tile">
          <div className="tile-label">Users</div>
          <div className="tile-value">{overview.totalUsers}</div>
          <div className="tile-sub">
            {overview.adminCount} {overview.adminCount === 1 ? 'admin' : 'admins'}
            {overview.disabledUsers > 0 && ` · ${overview.disabledUsers} disabled`}
          </div>
        </div>
        <div className="card tile">
          <div className="tile-label">Workouts, last 30 days</div>
          <div className="tile-value">{overview.workoutsLast30Days}</div>
          <div className="tile-sub">{overview.totalWorkouts} in total</div>
        </div>
      </div>

      <section className="card stack">
        <label className="switch-row">
          <input
            type="checkbox"
            className="switch"
          role="switch"
            checked={overview.registrationOpen}
            disabled={setOpen.isPending}
            onChange={(e) => setOpen.mutate(e.target.checked)}
          />
          <span>
            <strong>Allow new accounts to be created</strong>
            <span className="muted" style={{ display: 'block' }}>
              Turn this off once everyone you want has an account. Existing users are not affected.
            </span>
          </span>
        </label>
        {setOpen.error && <p className="error" role="alert">{setOpen.error.message}</p>}
      </section>
    </>
  )
}

function UsersSection() {
  const { data: users, isLoading, error } = useAdminUsers()

  return (
    <section className="card">
      <div className="card-title">
        <h2>Users</h2>
        <span className="muted">Workout counts only; admins can't see anyone's training.</span>
      </div>
      {isLoading && <PageSkeleton />}
      {error && <p className="error">{error.message}</p>}
      {users && (
        <ul className="list">
          {users.map((u) => (
            <UserRow key={u.id} user={u} />
          ))}
        </ul>
      )}
    </section>
  )
}

function UserRow({ user }: { user: AdminUser }) {
  const update = useUpdateAdminUser()
  const reset = useResetUserPassword()
  const remove = useDeleteAdminUser()
  const [resetting, setResetting] = useState(false)
  const [password, setPassword] = useState('')
  const [notice, setNotice] = useState<string | null>(null)

  const error = update.error ?? reset.error ?? remove.error

  async function submitReset(e: FormEvent) {
    e.preventDefault()
    try {
      await reset.mutateAsync({ id: user.id, newPassword: password })
      setResetting(false)
      setPassword('')
      setNotice(`Password reset. ${user.username} has been signed out everywhere.`)
    } catch {
      // Shown from reset.error.
    }
  }

  return (
    <li className="stack" style={{ padding: '12px 4px' }}>
      <div className="row" style={{ justifyContent: 'space-between', alignItems: 'flex-start' }}>
        <div>
          <strong>{user.username}</strong>{' '}
          {user.isYou && <span className="pill">You</span>}{' '}
          {user.isAdmin && <span className="pill">Admin</span>}{' '}
          {user.isDisabled && <span className="pill pill-danger">Disabled</span>}
          <p className="muted">
            Joined {formatDate(user.createdAt)} · {user.workoutCount} {user.workoutCount === 1 ? 'workout' : 'workouts'}
            {user.lastWorkoutAt && ` · last trained ${formatDate(user.lastWorkoutAt)}`}
          </p>
        </div>
        {!user.isYou && (
          <div className="row">
            <button
              className="btn small"
              disabled={update.isPending}
              onClick={() => update.mutate({ id: user.id, isAdmin: !user.isAdmin })}
            >
              {user.isAdmin ? 'Remove admin' : 'Make admin'}
            </button>
            <button
              className="btn small"
              disabled={update.isPending}
              onClick={() => update.mutate({ id: user.id, isDisabled: !user.isDisabled })}
            >
              {user.isDisabled ? 'Enable' : 'Disable'}
            </button>
            <button className="btn small" onClick={() => setResetting((v) => !v)}>
              Reset password
            </button>
            <button
              className="btn small danger"
              disabled={remove.isPending}
              onClick={() => {
                if (confirm(`Permanently delete ${user.username} and all their data? This cannot be undone.`)) remove.mutate(user.id)
              }}
            >
              Delete
            </button>
          </div>
        )}
      </div>

      {resetting && (
        <form className="row" onSubmit={submitReset}>
          <input
            className="grow"
            type="text"
            autoComplete="off"
            placeholder="New password (at least 8 characters)"
            aria-label={`New password for ${user.username}`}
            minLength={8}
            maxLength={128}
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
          />
          <button className="btn primary" disabled={reset.isPending}>
            Set password
          </button>
        </form>
      )}
      {notice && <p className="success" role="status">{notice}</p>}
      {error && <p className="error" role="alert">{error.message}</p>}
    </li>
  )
}
