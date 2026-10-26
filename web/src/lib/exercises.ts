import type { Exercise } from '../api/types'

export function groupByMuscle(exercises: Exercise[]): [string, Exercise[]][] {
  const groups = new Map<string, Exercise[]>()
  for (const e of exercises) groups.set(e.muscleGroup, [...(groups.get(e.muscleGroup) ?? []), e])
  return [...groups.entries()].sort(([a], [b]) => a.localeCompare(b))
}
