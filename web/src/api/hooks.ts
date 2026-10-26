import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from './client'
import type {
  AdminOverview,
  AdminUser,
  AuthResponse,
  BodyWeightEntry,
  CommunityMember,
  CommunityProfile,
  CreatedInvite,
  Invite,
  Exercise,
  ExerciseProgress,
  Overview,
  ProgressExercise,
  Template,
  TemplateInput,
  Workout,
  WorkoutDraft,
  WorkoutInput,
  WorkoutSummary,
} from './types'

export function useExercises() {
  return useQuery({ queryKey: ['exercises'], queryFn: () => api<Exercise[]>('/exercises') })
}

export function useCreateExercise() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: { name: string; muscleGroup: string }) =>
      api<Exercise>('/exercises', { method: 'POST', body: input }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['exercises'] }),
  })
}

export function useTemplates() {
  return useQuery({ queryKey: ['templates'], queryFn: () => api<Template[]>('/templates') })
}

export function useTemplate(id: string | undefined) {
  return useQuery({
    queryKey: ['templates', id],
    queryFn: () => api<Template>(`/templates/${id}`),
    enabled: id !== undefined,
  })
}

export function useSaveTemplate(id: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: TemplateInput) =>
      id
        ? api<Template>(`/templates/${id}`, { method: 'PUT', body: input })
        : api<Template>('/templates', { method: 'POST', body: input }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['templates'] }),
  })
}

export function useDeleteTemplate() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api<void>(`/templates/${id}`, { method: 'DELETE' }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['templates'] }),
  })
}

export function useWorkouts() {
  return useQuery({ queryKey: ['workouts'], queryFn: () => api<WorkoutSummary[]>('/workouts?limit=100') })
}

export function useWorkout(id: string | undefined) {
  return useQuery({
    queryKey: ['workouts', id],
    queryFn: () => api<Workout>(`/workouts/${id}`),
    enabled: id !== undefined,
  })
}

export function useWorkoutDraft(templateId: string | null) {
  return useQuery({
    queryKey: ['workout-draft', templateId],
    queryFn: () => api<WorkoutDraft>(`/workouts/draft/${templateId}`),
    enabled: templateId !== null,
    // A draft reflects the last logged weights, so it must be fresh each time a session starts.
    staleTime: 0,
    gcTime: 0,
  })
}

export function useSaveWorkout(id: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: WorkoutInput) =>
      id
        ? api<Workout>(`/workouts/${id}`, { method: 'PUT', body: input })
        : api<Workout>('/workouts', { method: 'POST', body: input }),
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: ['workouts'] }),
        queryClient.invalidateQueries({ queryKey: ['progress'] }),
      ]),
  })
}

export function useDeleteWorkout() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api<void>(`/workouts/${id}`, { method: 'DELETE' }),
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: ['workouts'] }),
        queryClient.invalidateQueries({ queryKey: ['progress'] }),
      ]),
  })
}

export function useBodyWeight() {
  return useQuery({ queryKey: ['body-weight'], queryFn: () => api<BodyWeightEntry[]>('/body-weight') })
}

export function useLogBodyWeight() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: { date: string; weightKg: number }) =>
      api<BodyWeightEntry>('/body-weight', { method: 'POST', body: input }),
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: ['body-weight'] }),
        queryClient.invalidateQueries({ queryKey: ['progress', 'overview'] }),
        queryClient.invalidateQueries({ queryKey: ['nutrition'] }),
      ]),
  })
}

export function useDeleteBodyWeight() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api<void>(`/body-weight/${id}`, { method: 'DELETE' }),
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: ['body-weight'] }),
        queryClient.invalidateQueries({ queryKey: ['progress', 'overview'] }),
        queryClient.invalidateQueries({ queryKey: ['nutrition'] }),
      ]),
  })
}

export function useProgressExercises() {
  return useQuery({
    queryKey: ['progress', 'exercises'],
    queryFn: () => api<ProgressExercise[]>('/progress/exercises'),
  })
}

export function useExerciseProgress(exerciseId: string | undefined) {
  return useQuery({
    queryKey: ['progress', 'exercise', exerciseId],
    queryFn: () => api<ExerciseProgress>(`/progress/exercises/${exerciseId}`),
    enabled: exerciseId !== undefined,
    placeholderData: (previous) => previous,
  })
}

export function useOverview() {
  return useQuery({ queryKey: ['progress', 'overview'], queryFn: () => api<Overview>('/progress/overview?weeks=12') })
}

export function useCommunitySettings() {
  return useQuery({
    queryKey: ['community', 'settings'],
    queryFn: () => api<{ shareActivity: boolean }>('/community/settings'),
  })
}

export function useUpdateCommunitySettings() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (shareActivity: boolean) =>
      api<{ shareActivity: boolean }>('/community/settings', { method: 'PUT', body: { shareActivity } }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['community'] }),
  })
}

export function useCommunityMembers() {
  return useQuery({ queryKey: ['community', 'users'], queryFn: () => api<CommunityMember[]>('/community/users') })
}

export function useCommunityProfile(username: string | undefined) {
  return useQuery({
    queryKey: ['community', 'users', username],
    queryFn: () => api<CommunityProfile>(`/community/users/${encodeURIComponent(username!)}`),
    enabled: username !== undefined,
  })
}

export function useAuthConfig() {
  return useQuery({
    queryKey: ['auth', 'config'],
    queryFn: () => api<{ registrationOpen: boolean }>('/auth/config'),
  })
}

export function useChangePassword() {
  return useMutation({
    mutationFn: (input: { currentPassword: string; newPassword: string }) =>
      api<AuthResponse>('/account/password', { method: 'PUT', body: input }),
  })
}

export function useDeleteAccount() {
  return useMutation({
    mutationFn: (password: string) => api<void>('/account/delete', { method: 'POST', body: { password } }),
  })
}

export function useAdminOverview() {
  return useQuery({ queryKey: ['admin', 'overview'], queryFn: () => api<AdminOverview>('/admin/overview') })
}

export function useAdminUsers() {
  return useQuery({ queryKey: ['admin', 'users'], queryFn: () => api<AdminUser[]>('/admin/users') })
}

function useAdminMutation<TInput, TResult>(fn: (input: TInput) => Promise<TResult>) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: fn,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin'] }),
  })
}

export const useUpdateAdminUser = () =>
  useAdminMutation((input: { id: string; isAdmin?: boolean; isDisabled?: boolean }) =>
    api<AdminUser>(`/admin/users/${input.id}`, {
      method: 'PUT',
      body: { isAdmin: input.isAdmin, isDisabled: input.isDisabled },
    }),
  )

export const useResetUserPassword = () =>
  useAdminMutation((input: { id: string; newPassword: string }) =>
    api<void>(`/admin/users/${input.id}/reset-password`, { method: 'POST', body: { newPassword: input.newPassword } }),
  )

export const useDeleteAdminUser = () =>
  useAdminMutation((id: string) => api<void>(`/admin/users/${id}`, { method: 'DELETE' }))

export const useSetRegistrationOpen = () =>
  useAdminMutation((registrationOpen: boolean) =>
    api<{ registrationOpen: boolean }>('/admin/settings', { method: 'PUT', body: { registrationOpen } }),
  )

export function useInvites() {
  return useQuery({ queryKey: ['admin', 'invites'], queryFn: () => api<Invite[]>('/admin/invites') })
}

export const useCreateInvite = () =>
  useAdminMutation((input: { note: string | null; expiresInDays: number | null }) =>
    api<CreatedInvite>('/admin/invites', { method: 'POST', body: input }),
  )

export const useRevokeInvite = () =>
  useAdminMutation((id: string) => api<void>(`/admin/invites/${id}`, { method: 'DELETE' }))
