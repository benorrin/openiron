import { createContext, useContext } from 'react'
import type { AuthResponse, User } from '../api/types'

export interface AuthState {
  user: User | null
  /** True until the stored token (if any) has been checked against the API. */
  loading: boolean
  login: (username: string, password: string) => Promise<void>
  register: (username: string, password: string, inviteCode?: string) => Promise<void>
  /** Adopts a freshly issued session, e.g. after a password change replaces the old token. */
  applySession: (auth: AuthResponse) => void
  logout: () => void
}

export const AuthContext = createContext<AuthState | null>(null)

export function useAuth(): AuthState {
  const context = useContext(AuthContext)
  if (!context) throw new Error('useAuth must be used inside AuthProvider')
  return context
}
