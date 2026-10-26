import type { ReactNode } from 'react'
import { Icon, type IconName } from './Icon'

interface Props {
  icon: IconName
  title: string
  children?: ReactNode
  action?: ReactNode
}

export function EmptyState({ icon, title, children, action }: Props) {
  return (
    <div className="card empty-state">
      <span className="icon-chip">
        <Icon name={icon} size={26} />
      </span>
      <h2>{title}</h2>
      {children && <p>{children}</p>}
      {action}
    </div>
  )
}
