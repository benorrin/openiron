import { NavLink } from 'react-router-dom'

/** Weight and Nutrition share one tab on a phone, so this switches between them. Hidden on wide screens, where the sidebar has both. */
export function BodyTabs() {
  return (
    <nav className="tabs body-tabs" aria-label="Body">
      <NavLink to="/body-weight">Weight</NavLink>
      <NavLink to="/nutrition">Nutrition</NavLink>
    </nav>
  )
}
