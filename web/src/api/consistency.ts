import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from './client'

export type ActivityType = 'walk' | 'run' | 'treadmill' | 'cycling' | 'swim' | 'hike' | 'other' | 'steps'

export interface Activity {
  id: string
  date: string
  type: ActivityType
  durationMinutes: number | null
  distanceKm: number | null
  steps: number | null
  notes: string | null
}

export interface ActivityInput {
  date: string
  type: ActivityType
  durationMinutes: number | null
  distanceKm: number | null
  steps: number | null
  notes: string | null
}

/** Sunday is 0, as in JavaScript's Date.getDay(). */
export interface ScheduledDay {
  dayOfWeek: number
  templateId: string | null
  templateName: string | null
}

export interface Schedule {
  days: ScheduledDay[]
  stepGoal: number
  since: string | null
}

export type DayState = 'done' | 'missed' | 'pending' | 'upcoming' | 'rest' | 'bonus'

export interface WeekDay {
  date: string
  dayOfWeek: number
  isToday: boolean
  state: DayState
  templateId: string | null
  templateName: string | null
  sessions: number
  steps: number | null
}

export interface WeekSummary {
  weekStart: string
  scheduled: number
  due: number
  completed: number
  activeDays: number
  percent: number | null
}

export interface HeatCell {
  date: string
  level: number
  workouts: number
  activities: number
  minutes: number
  steps: number | null
}

export interface Consistency {
  today: string
  stepGoal: number
  hasSchedule: boolean
  week: WeekDay[]
  weeks: WeekSummary[]
  summary: {
    activeDays30: number
    activeDays90: number
    activeDays365: number
    adherencePercent4Weeks: number | null
    streak: { current: number; best: number }
  }
  heatmapStart: string
  heatmap: HeatCell[]
}

/** Minutes the browser's clock is from UTC (UTC+1 is -60), so the server can put each day on this user's calendar. */
const tz = () => new Date().getTimezoneOffset()

export function useConsistency() {
  return useQuery({ queryKey: ['consistency'], queryFn: () => api<Consistency>(`/consistency?tz=${tz()}`) })
}

export function useSchedule() {
  return useQuery({ queryKey: ['schedule'], queryFn: () => api<Schedule>('/schedule') })
}

export function useSaveSchedule() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: { days: { dayOfWeek: number; templateId: string | null }[]; stepGoal: number }) =>
      api<Schedule>(`/schedule?tz=${tz()}`, { method: 'PUT', body: input }),
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: ['schedule'] }),
        queryClient.invalidateQueries({ queryKey: ['consistency'] }),
      ]),
  })
}

export function useActivities() {
  return useQuery({ queryKey: ['activities'], queryFn: () => api<Activity[]>('/activities') })
}

function useActivityMutation<TInput>(fn: (input: TInput) => Promise<unknown>) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: fn,
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: ['activities'] }),
        queryClient.invalidateQueries({ queryKey: ['consistency'] }),
      ]),
  })
}

export const useLogActivity = () =>
  useActivityMutation((input: ActivityInput) => api<Activity>('/activities', { method: 'POST', body: input }))

export const useDeleteActivity = () =>
  useActivityMutation((id: string) => api<void>(`/activities/${id}`, { method: 'DELETE' }))
