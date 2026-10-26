import { useQueryClient } from '@tanstack/react-query'
import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { api, getToken, setToken, setUnauthorizedHandler } from '../api/client'
import type { AuthResponse, User } from '../api/types'
import { AuthContext, type AuthState } from './useAuth'

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const [user, setUser] = useState<User | null>(null)
  const [loading, setLoading] = useState(() => getToken() !== null)

  const logout = useCallback(() => {
    setToken(null)
    setUser(null)
    queryClient.clear()
  }, [queryClient])

  useEffect(() => {
    setUnauthorizedHandler(logout)
    if (!getToken()) return
    api<User>('/auth/me')
      .then(setUser)
      .catch(() => setToken(null))
      .finally(() => setLoading(false))
  }, [logout])

  const applySession = useCallback((auth: AuthResponse) => {
    setToken(auth.token)
    setUser(auth.user)
  }, [])

  const authenticate = useCallback(
    async (path: string, body: { username: string; password: string; inviteCode?: string }) => {
      const auth = await api<AuthResponse>(path, { method: 'POST', body })
      queryClient.clear()
      applySession(auth)
    },
    [queryClient, applySession],
  )

  const value = useMemo<AuthState>(
    () => ({
      user,
      loading,
      login: (username, password) => authenticate('/auth/login', { username, password }),
      register: (username, password, inviteCode) =>
        authenticate('/auth/register', { username, password, inviteCode: inviteCode?.trim() || undefined }),
      applySession,
      logout,
    }),
    [user, loading, authenticate, applySession, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
