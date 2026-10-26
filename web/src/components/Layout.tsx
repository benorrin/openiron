import { NavLink, Outlet } from 'react-router-dom'
import { useAuth } from '../auth/useAuth'

const links = [
  { to: '/', label: 'Home', end: true },
  { to: '/workouts', label: 'Workouts' },
  { to: '/templates', label: 'Templates' },
  { to: '/progress', label: 'Progress' },
  { to: '/body-weight', label: 'Weight' },
  { to: '/community', label: 'Community' },
]

export function Layout() {
  const { user, logout } = useAuth()

  return (
    <div className="app">
      <header className="topbar">
        <div className="topbar-inner">
          <NavLink to="/" className="brand">
            <img src="/favicon.svg" alt="" />
            OpenIron
          </NavLink>
          <nav className="nav" aria-label="Main">
            {links.map((l) => (
              <NavLink key={l.to} to={l.to} end={l.end}>
                {l.label}
              </NavLink>
            ))}
          </nav>
        </div>
        <div className="row">
          <span className="muted">{user?.username}</span>
          <button className="btn small ghost" onClick={logout}>
            Log out
          </button>
        </div>
      </header>
      <main>
        <Outlet />
      </main>
    </div>
  )
}
