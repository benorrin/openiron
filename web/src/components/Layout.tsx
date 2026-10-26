import { NavLink, Outlet } from 'react-router-dom'
import { useAuth } from '../auth/useAuth'
import { Icon, type IconName } from './Icon'

const links: { to: string; label: string; icon: IconName; end?: boolean }[] = [
  { to: '/', label: 'Home', icon: 'home', end: true },
  { to: '/workouts', label: 'Workouts', icon: 'dumbbell' },
  { to: '/templates', label: 'Templates', icon: 'clipboard' },
  { to: '/progress', label: 'Progress', icon: 'trending' },
  { to: '/body-weight', label: 'Weight', icon: 'scale' },
  { to: '/community', label: 'Community', icon: 'users' },
]

function Brand() {
  return (
    <NavLink to="/" className="brand">
      <img src="/favicon.svg" alt="" />
      OpenIron
    </NavLink>
  )
}

/**
 * The same navigation is rendered twice, as a sidebar and as a mobile header plus tab bar. CSS shows one
 * set at a time, so the hidden one is also out of the accessibility tree.
 */
export function Layout() {
  const { user, logout } = useAuth()
  const initial = user?.username.charAt(0).toUpperCase()

  return (
    <div className="shell">
      <aside className="sidebar">
        <Brand />
        <nav className="side-nav" aria-label="Main">
          {links.map((l) => (
            <NavLink key={l.to} to={l.to} end={l.end} className="side-link">
              <Icon name={l.icon} />
              {l.label}
            </NavLink>
          ))}
        </nav>
        <div className="side-footer">
          <div className="user-chip">
            <span className="avatar" aria-hidden="true">
              {initial}
            </span>
            <span className="user-name">{user?.username}</span>
          </div>
          {user?.isAdmin && (
            <NavLink to="/admin" className="side-link">
              <Icon name="shield" />
              Admin
            </NavLink>
          )}
          <NavLink to="/settings" className="side-link">
            <Icon name="sliders" />
            Settings
          </NavLink>
          <button className="side-link" onClick={logout}>
            <Icon name="logout" />
            Log out
          </button>
        </div>
      </aside>

      <header className="mobile-header">
        <Brand />
        <div className="row" style={{ gap: 2, flexWrap: 'nowrap' }}>
          {user?.isAdmin && (
            <NavLink to="/admin" className="icon-btn" aria-label="Admin">
              <Icon name="shield" />
            </NavLink>
          )}
          <NavLink to="/settings" className="icon-btn" aria-label="Settings">
            <Icon name="sliders" />
          </NavLink>
          <button className="icon-btn" onClick={logout} aria-label="Log out">
            <Icon name="logout" />
          </button>
        </div>
      </header>

      <main className="content">
        <Outlet />
      </main>

      <nav className="tabbar" aria-label="Main (mobile)">
        {links.map((l) => (
          <NavLink key={l.to} to={l.to} end={l.end} className="tab">
            <Icon name={l.icon} size={22} />
            {l.label}
          </NavLink>
        ))}
      </nav>
    </div>
  )
}
