import { NavLink, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '../auth/useAuth'
import { Icon, type IconName } from './Icon'

interface NavItem {
  to: string
  label: string
  icon: IconName
  end?: boolean
  /** A shorter or broader name for the phone tab bar, which has less room. */
  tabLabel?: string
  /** Other routes that should light up this tab. */
  alsoActiveOn?: string[]
  /** Reached from another tab on a phone rather than having its own. */
  sidebarOnly?: boolean
}

const links: NavItem[] = [
  { to: '/', label: 'Home', icon: 'home', end: true },
  { to: '/workouts', label: 'Workouts', icon: 'dumbbell', alsoActiveOn: ['/activity'] },
  { to: '/activity', label: 'Activity', icon: 'pulse', sidebarOnly: true },
  { to: '/templates', label: 'Templates', icon: 'clipboard' },
  { to: '/progress', label: 'Progress', icon: 'trending', alsoActiveOn: ['/consistency'] },
  { to: '/consistency', label: 'Consistency', icon: 'grid', sidebarOnly: true },
  { to: '/body-weight', label: 'Weight', icon: 'scale', tabLabel: 'Body', alsoActiveOn: ['/nutrition'] },
  { to: '/nutrition', label: 'Nutrition', icon: 'flame', sidebarOnly: true },
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
  const { pathname } = useLocation()

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
        {links
          .filter((l) => !l.sidebarOnly)
          .map((l) => (
            <NavLink
              key={l.to}
              to={l.to}
              end={l.end}
              className={({ isActive }) =>
                `tab${isActive || l.alsoActiveOn?.some((p) => pathname.startsWith(p)) ? ' active' : ''}`
              }
            >
              <Icon name={l.icon} size={22} />
              {l.tabLabel ?? l.label}
            </NavLink>
          ))}
      </nav>
    </div>
  )
}
