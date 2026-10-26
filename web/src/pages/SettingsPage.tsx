import { useState, type FormEvent } from 'react'
import {
  useChangePassword,
  useCommunitySettings,
  useDeleteAccount,
  useUpdateCommunitySettings,
} from '../api/hooks'
import { useAuth } from '../auth/useAuth'

export function SettingsPage() {
  return (
    <div className="page">
      <h1>Settings</h1>
      <AccountSection />
      <PrivacySection />
      <DeleteAccountSection />
    </div>
  )
}

function AccountSection() {
  const { user, applySession } = useAuth()
  const change = useChangePassword()
  const [current, setCurrent] = useState('')
  const [next, setNext] = useState('')
  const [confirm, setConfirm] = useState('')
  const [done, setDone] = useState(false)

  const mismatch = confirm !== '' && confirm !== next

  async function submit(e: FormEvent) {
    e.preventDefault()
    setDone(false)
    try {
      const auth = await change.mutateAsync({ currentPassword: current, newPassword: next })
      // Changing the password signs out every other session; keep this one going on the new token.
      applySession(auth)
      setCurrent('')
      setNext('')
      setConfirm('')
      setDone(true)
    } catch {
      // Shown from change.error.
    }
  }

  return (
    <section className="card stack">
      <h2>Account</h2>
      <p className="muted">
        Signed in as <strong>{user?.username}</strong>
        {user?.isAdmin && ' (admin)'}
      </p>

      <form className="stack" onSubmit={submit}>
        <h3>Change password</h3>
        <label className="field">
          Current password
          <input type="password" autoComplete="current-password" value={current} onChange={(e) => setCurrent(e.target.value)} required />
        </label>
        <label className="field">
          New password
          <input type="password" autoComplete="new-password" minLength={8} maxLength={128} value={next} onChange={(e) => setNext(e.target.value)} required />
          <span className="muted">At least 8 characters. Changing it signs you out on your other devices.</span>
        </label>
        <label className="field">
          Confirm new password
          <input type="password" autoComplete="new-password" value={confirm} onChange={(e) => setConfirm(e.target.value)} required />
        </label>
        {mismatch && <p className="error">The passwords don't match.</p>}
        {change.error && <p className="error" role="alert">{change.error.message}</p>}
        {done && <p className="success" role="status">Password changed.</p>}
        <div>
          <button className="btn primary" disabled={change.isPending || mismatch}>
            Change password
          </button>
        </div>
      </form>
    </section>
  )
}

function PrivacySection() {
  const { data: settings } = useCommunitySettings()
  const update = useUpdateCommunitySettings()

  return (
    <section className="card stack">
      <h2>Privacy</h2>
      <label className="switch-row">
        <input
          type="checkbox"
          className="switch"
          role="switch"
          checked={settings?.shareActivity ?? false}
          disabled={!settings || update.isPending}
          onChange={(e) => update.mutate(e.target.checked)}
        />
        <span>
          <strong>Share my training with others on this server</strong>
          <span className="muted" style={{ display: 'block' }}>
            Other members can see your workout counts, volume, main lifts and recent workout names on the Community page.
            Your body weight and notes are never shared. This is off by default.
          </span>
        </span>
      </label>
      {update.error && <p className="error" role="alert">{update.error.message}</p>}
    </section>
  )
}

function DeleteAccountSection() {
  const { logout } = useAuth()
  const remove = useDeleteAccount()
  const [password, setPassword] = useState('')

  async function submit(e: FormEvent) {
    e.preventDefault()
    if (!confirm('Permanently delete your account and all your workouts, templates and body weight entries? This cannot be undone.')) return
    try {
      await remove.mutateAsync(password)
      logout()
    } catch {
      // Shown from remove.error.
    }
  }

  return (
    <section className="card stack danger-zone">
      <h2>Delete account</h2>
      <p className="muted">
        Permanently removes your account and everything you've logged: workouts, templates, custom exercises and body
        weight. There is no undo.
      </p>
      <form className="row" onSubmit={submit} style={{ alignItems: 'flex-end' }}>
        <label className="field grow">
          Confirm with your password
          <input type="password" autoComplete="current-password" value={password} onChange={(e) => setPassword(e.target.value)} required />
        </label>
        <button className="btn danger" disabled={remove.isPending}>
          Delete my account
        </button>
      </form>
      {remove.error && <p className="error" role="alert">{remove.error.message}</p>}
    </section>
  )
}
