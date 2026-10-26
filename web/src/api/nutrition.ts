import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from './client'

export type Sex = 'male' | 'female' | 'unspecified'
export type ActivityLevel = 'sedentary' | 'light' | 'moderate' | 'active' | 'veryActive'
export type GoalType = 'lose' | 'maintain' | 'gain'

export type ProgressStatus =
  | 'noPlan'
  | 'gathering'
  | 'onTrack'
  | 'fasterThanPlanned'
  | 'slowerThanPlanned'
  | 'wrongDirection'
  | 'driftingUp'
  | 'driftingDown'

export interface NutritionSettings {
  sex: Sex
  dateOfBirth: string
  heightCm: number
  activity: ActivityLevel
  goal: GoalType
  targetWeightKg: number | null
  weeklyRateKg: number
  appliedKcal: number | null
  appliedOn: string | null
}

export type NutritionSettingsInput = Omit<NutritionSettings, 'appliedKcal' | 'appliedOn'>

export interface Macros {
  proteinG: number
  fatG: number
  carbsG: number
}

export interface PlanProgress {
  status: ProgressStatus
  observedRateKgPerWeek: number | null
  plannedRateKgPerWeek: number
  weighInsUsed: number
  daysOfData: number
  daysUntilCheck: number | null
  suggestedAdjustmentKcal: number | null
  remainingKg: number | null
  goalReached: boolean
  estimatedGoalDate: string | null
}

export interface Plan {
  currentWeightKg: number
  age: number
  bmrKcal: number
  formulaMaintenanceKcal: number
  estimatedMaintenanceKcal: number
  confidence: number
  targetKcal: number
  limitedBySafetyFloor: boolean
  rateLimited: boolean
  macros: Macros
  progress: PlanProgress
  trend: { date: string; weightKg: number }[]
}

export interface Nutrition {
  settings: NutritionSettings | null
  plan: Plan | null
  planUnavailableReason: 'needsSettings' | 'needsWeighIn' | null
}

const KEY = ['nutrition']

export function useNutrition() {
  return useQuery({ queryKey: KEY, queryFn: () => api<Nutrition>('/nutrition') })
}

/** Each of these returns the refreshed nutrition state, so the page updates without another request. */
function useNutritionMutation<TInput>(fn: (input: TInput) => Promise<Nutrition>) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: fn,
    onSuccess: (data) => queryClient.setQueryData(KEY, data),
  })
}

export const useSaveNutritionSettings = () =>
  useNutritionMutation((input: NutritionSettingsInput) =>
    api<Nutrition>('/nutrition/settings', { method: 'PUT', body: input }),
  )

/** With no calories, applies the current recommendation. */
export const useApplyTarget = () =>
  useNutritionMutation((calories?: number) =>
    api<Nutrition>('/nutrition/apply', { method: 'POST', body: { calories: calories ?? null } }),
  )

export function useClearApplied() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => api<Nutrition>('/nutrition/apply', { method: 'DELETE' }),
    onSuccess: (data) => queryClient.setQueryData(KEY, data),
  })
}
