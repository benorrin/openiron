import { useState, type FormEvent } from 'react'
import { useAuthConfig } from '../api/hooks'
import { useAuth } from '../auth/useAuth'

export function LoginPage() {
  const { login, register } = useAuth()
  const { data: config } = useAuthConfig()
  const registrationOpen = config?.registrationOpen ?? true
  const [chosen, setMode] = useState<'login' | 'register'>('login')
  const mode = registrationOpen ? chosen : 'login'
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function submit(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      await (mode === 'login' ? login : register)(username, password)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Something went wrong')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="auth">
      <div className="brand">
        <img src="/favicon.svg" alt="" />
        OpenIron
      </div>
      {registrationOpen ? (
        <div className="tabs" role="tablist">
          <button role="tab" aria-selected={mode === 'login'} onClick={() => setMode('login')}>
            Log in
          </button>
          <button role="tab" aria-selected={mode === 'register'} onClick={() => setMode('register')}>
            Create account
          </button>
        </div>
      ) : (
        <p className="muted" style={{ textAlign: 'center' }}>
          New accounts are closed on this server. Ask an admin if you need one.
        </p>
      )}
      <form className="card stack" onSubmit={submit}>
        <label className="field">
          Username
          <input
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            autoComplete="username"
            autoCapitalize="none"
            required
          />
        </label>
        <label className="field">
          Password
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            autoComplete={mode === 'login' ? 'current-password' : 'new-password'}
            minLength={mode === 'register' ? 8 : undefined}
            required
          />
          {mode === 'register' && <span className="muted">At least 8 characters.</span>}
        </label>
        {error && (
          <p className="error" role="alert">
            {error}
          </p>
        )}
        <button className="btn primary" disabled={busy}>
          {mode === 'login' ? 'Log in' : 'Create account'}
        </button>
      </form>
    </div>
  )
}
