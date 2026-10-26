import { useEffect, useState, type FormEvent } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useAuthConfig } from '../api/hooks'
import { Icon } from '../components/Icon'
import { useAuth } from '../auth/useAuth'

export function LoginPage() {
  const { login, register } = useAuth()
  const { data: config } = useAuthConfig()
  const registrationOpen = config?.registrationOpen ?? true
  // An invite link (/?invite=CODE) lands straight on the sign-up form with the code filled in.
  const [search] = useSearchParams()
  const linkedCode = search.get('invite') ?? ''
  const [mode, setMode] = useState<'login' | 'register'>(linkedCode ? 'register' : 'login')
  const [inviteCode, setInviteCode] = useState(linkedCode)
  const needsInvite = mode === 'register' && !registrationOpen

  // The code is in state now; don't leave it sitting in the address bar and browser history.
  useEffect(() => {
    if (linkedCode) window.history.replaceState(null, '', window.location.pathname)
  }, [linkedCode])
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function submit(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      if (mode === 'login') await login(username, password)
      else await register(username, password, needsInvite ? inviteCode : undefined)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Something went wrong')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="auth-layout">
      <aside className="auth-aside">
        <div className="brand">
          <img src="/favicon.svg" alt="" />
          OpenIron
        </div>
        <div className="stack" style={{ gap: 20 }}>
          <h1>Your training, on your own server.</h1>
          <ul className="auth-points">
            <li>
              <Icon name="clipboard" /> Plan sessions as templates
            </li>
            <li>
              <Icon name="dumbbell" /> Log every set in seconds
            </li>
            <li>
              <Icon name="trending" /> Watch your lifts and body weight trend
            </li>
          </ul>
        </div>
        <p>Self-hosted and open source. Your data never leaves your server.</p>
      </aside>
      <div className="auth">
        <div className="brand">
          <img src="/favicon.svg" alt="" />
          OpenIron
        </div>
        <div className="tabs" role="tablist">
          <button role="tab" aria-selected={mode === 'login'} onClick={() => setMode('login')}>
            Log in
          </button>
          <button role="tab" aria-selected={mode === 'register'} onClick={() => setMode('register')}>
            Create account
          </button>
        </div>
        <form className="card stack" onSubmit={submit}>
          {needsInvite && (
            <label className="field">
              Invite code
              <input
                value={inviteCode}
                onChange={(e) => setInviteCode(e.target.value)}
                className="code-input"
                placeholder="XXXX-XXXX-XXXX"
                autoComplete="off"
                autoCapitalize="characters"
                spellCheck={false}
                required
              />
              <span className="muted">Sign-up on this server is by invitation. Enter the code you were given.</span>
            </label>
          )}
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
    </div>
  )
}
