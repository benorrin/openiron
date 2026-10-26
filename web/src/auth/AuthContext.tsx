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

  const authenticate = useCallback(
    async (path: string, username: string, password: string) => {
      const auth = await api<AuthResponse>(path, { method: 'POST', body: { username, password } })
      queryClient.clear()
      setToken(auth.token)
      setUser(auth.user)
    },
    [queryClient],
  )

  const value = useMemo<AuthState>(
    () => ({
      user,
      loading,
      login: (u, p) => authenticate('/auth/login', u, p),
      register: (u, p) => authenticate('/auth/register', u, p),
      logout,
    }),
    [user, loading, authenticate, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
