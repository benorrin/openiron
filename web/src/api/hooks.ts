import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from './client'
import type { Exercise, Template, TemplateInput } from './types'

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
