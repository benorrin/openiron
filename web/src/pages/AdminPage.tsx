import { useState, type FormEvent } from 'react'
import { Navigate } from 'react-router-dom'
import {
  useAdminOverview,
  useCreateInvite,
  useInvites,
  useRevokeInvite,
  useAdminUsers,
  useDeleteAdminUser,
  useResetUserPassword,
  useSetRegistrationOpen,
  useUpdateAdminUser,
} from '../api/hooks'
import type { AdminUser, CreatedInvite } from '../api/types'
import { useAuth } from '../auth/useAuth'
import { formatDate } from '../lib/format'
import { copyText, inviteLink, inviteStatusLabel } from '../lib/invites'
import { PageSkeleton } from '../components/Skeleton'
import { Icon } from '../components/Icon'

export function AdminPage() {
  const { user } = useAuth()
  // The API enforces this too; this just keeps non-admins off a page that can't work for them.
  if (!user?.isAdmin) return <Navigate to="/" replace />

  return (
    <div className="page">
      <h1>Admin</h1>
      <OverviewSection />
      <InvitesSection />
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
          <span className="icon-chip"><Icon name="users" size={18} /></span>
          <div className="tile-label">Users</div>
          <div className="tile-value">{overview.totalUsers}</div>
          <div className="tile-sub">
            {overview.adminCount} {overview.adminCount === 1 ? 'admin' : 'admins'}
            {overview.disabledUsers > 0 && ` · ${overview.disabledUsers} disabled`}
          </div>
        </div>
        <div className="card tile">
          <span className="icon-chip"><Icon name="calendar" size={18} /></span>
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
            <strong>Anyone can create an account</strong>
            <span className="muted" style={{ display: 'block' }}>
              Turn this off to make sign-up invitation only: people then need a code from you to join. Existing users
              are not affected.
            </span>
          </span>
        </label>
        {setOpen.error && <p className="error" role="alert">{setOpen.error.message}</p>}
      </section>
    </>
  )
}

const EXPIRY_OPTIONS = [
  { label: '1 day', days: 1 },
  { label: '7 days', days: 7 },
  { label: '30 days', days: 30 },
  { label: 'Never expires', days: null },
]

function InvitesSection() {
  const { data: overview } = useAdminOverview()
  const { data: invites, isLoading, error } = useInvites()
  const create = useCreateInvite()
  const revoke = useRevokeInvite()
  const [note, setNote] = useState('')
  const [days, setDays] = useState<number | null>(7)
  const [fresh, setFresh] = useState<CreatedInvite | null>(null)

  async function submit(e: FormEvent) {
    e.preventDefault()
    try {
      setFresh(await create.mutateAsync({ note: note.trim() || null, expiresInDays: days }))
      setNote('')
    } catch {
      // Shown from create.error.
    }
  }

  return (
    <section className="card stack">
      <div className="card-title" style={{ marginBottom: 0 }}>
        <h2>Invites</h2>
        <span className="muted">One code, one person.</span>
      </div>
      {overview?.registrationOpen && (
        <p className="muted">
          Sign-up is open to anyone right now, so codes aren't needed. They apply once you turn off "Anyone can create an
          account".
        </p>
      )}

      <form className="row" onSubmit={submit} style={{ alignItems: 'flex-end' }}>
        <label className="field grow wide">
          Who is it for?
          <input value={note} onChange={(e) => setNote(e.target.value)} placeholder="Optional, e.g. Sam" maxLength={100} />
        </label>
        <label className="field grow">
          Expires
          <select value={days ?? ''} onChange={(e) => setDays(e.target.value === '' ? null : Number(e.target.value))}>
            {EXPIRY_OPTIONS.map((o) => (
              <option key={o.label} value={o.days ?? ''}>
                {o.label}
              </option>
            ))}
          </select>
        </label>
        <button className="btn primary" disabled={create.isPending}>
          <Icon name="plus" size={18} />
          Create invite
        </button>
      </form>
      {create.error && <p className="error" role="alert">{create.error.message}</p>}

      {fresh && <InviteReveal created={fresh} onDismiss={() => setFresh(null)} />}

      {isLoading && <div className="skeleton" style={{ height: 56 }} />}
      {error && <p className="error">{error.message}</p>}
      {invites && invites.length === 0 && <p className="muted">No invites yet.</p>}
      {invites && invites.length > 0 && (
        <ul className="list">
          {invites.map((i) => (
            <li key={i.id} className="row" style={{ padding: '10px 4px', justifyContent: 'space-between', flexWrap: 'nowrap' }}>
              <div>
                <strong>{i.note ?? 'No note'}</strong>{' '}
                <span className={i.status === 'active' ? 'pill' : 'pill pill-quiet'}>{inviteStatusLabel(i)}</span>
                <p className="muted">
                  Created {formatDate(i.createdAt)}
                  {i.status === 'active' && i.expiresAt && ` · expires ${formatDate(i.expiresAt)}`}
                </p>
              </div>
              {i.status === 'active' && (
                <button
                  className="btn small danger"
                  disabled={revoke.isPending}
                  onClick={() => {
                    if (confirm('Revoke this invite? The code will stop working.')) revoke.mutate(i.id)
                  }}
                >
                  Revoke
                </button>
              )}
            </li>
          ))}
        </ul>
      )}
      {revoke.error && <p className="error" role="alert">{revoke.error.message}</p>}
    </section>
  )
}

/** The one place a code is ever shown, so it is hard to miss and easy to copy. */
function InviteReveal({ created, onDismiss }: { created: CreatedInvite; onDismiss: () => void }) {
  const link = inviteLink(window.location.origin, created.code)
  const [copied, setCopied] = useState<'code' | 'link' | null>(null)

  async function copy(kind: 'code' | 'link') {
    const ok = await copyText(kind === 'code' ? created.code : link)
    setCopied(ok ? kind : null)
  }

  return (
    <div className="invite-reveal stack" role="status">
      <div className="row" style={{ justifyContent: 'space-between' }}>
        <strong>Invite created{created.invite.note ? ` for ${created.invite.note}` : ''}</strong>
        <button className="btn small ghost" onClick={onDismiss}>
          Done
        </button>
      </div>
      <p className="muted">Copy it now: for security the code isn't stored and can't be shown again.</p>
      <input className="code-input code-display" readOnly value={created.code} aria-label="Invite code" onFocus={(e) => e.target.select()} />
      <div className="row">
        <button className="btn small" onClick={() => copy('code')}>
          {copied === 'code' ? 'Copied' : 'Copy code'}
        </button>
        <button className="btn small" onClick={() => copy('link')}>
          {copied === 'link' ? 'Copied' : 'Copy invite link'}
        </button>
      </div>
    </div>
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
