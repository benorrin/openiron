import { Navigate, Route, Routes } from 'react-router-dom'
import { useAuth } from './auth/useAuth'
import { Layout } from './components/Layout'
import { AdminPage } from './pages/AdminPage'
import { BodyWeightPage } from './pages/BodyWeightPage'
import { CommunityPage } from './pages/CommunityPage'
import { CommunityProfilePage } from './pages/CommunityProfilePage'
import { HomePage } from './pages/HomePage'
import { LoginPage } from './pages/LoginPage'
import { WorkoutEditPage } from './pages/WorkoutEditPage'
import { WorkoutsPage } from './pages/WorkoutsPage'
import { ProgressPage } from './pages/ProgressPage'
import { SettingsPage } from './pages/SettingsPage'
import { TemplateEditPage } from './pages/TemplateEditPage'
import { TemplatesPage } from './pages/TemplatesPage'

export function App() {
  const { user, loading } = useAuth()

  if (loading) return <p className="empty">Loading…</p>
  if (!user) return <LoginPage />

  return (
    <Routes>
      <Route element={<Layout />}>
        <Route index element={<HomePage />} />
        <Route path="templates" element={<TemplatesPage />} />
        <Route path="templates/new" element={<TemplateEditPage />} />
        <Route path="templates/:id" element={<TemplateEditPage />} />
        <Route path="workouts" element={<WorkoutsPage />} />
        <Route path="workouts/new" element={<WorkoutEditPage />} />
        <Route path="workouts/:id" element={<WorkoutEditPage />} />
        <Route path="progress" element={<ProgressPage />} />
        <Route path="body-weight" element={<BodyWeightPage />} />
        <Route path="community" element={<CommunityPage />} />
        <Route path="community/:username" element={<CommunityProfilePage />} />
        <Route path="admin" element={<AdminPage />} />
        <Route path="settings" element={<SettingsPage />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  )
}
