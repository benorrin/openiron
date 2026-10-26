import { expect, it } from 'vitest'
import type { Exercise } from '../api/types'
import { groupByMuscle } from './exercises'

it('groups exercises by muscle group, alphabetically, keeping order within a group', () => {
  const ex = (name: string, muscleGroup: string): Exercise => ({ id: name, name, muscleGroup, isCustom: false })
  const groups = groupByMuscle([ex('Squat', 'Legs'), ex('Row', 'Back'), ex('Lunge', 'Legs')])

  expect(groups.map(([g]) => g)).toEqual(['Back', 'Legs'])
  expect(groups[1][1].map((e) => e.name)).toEqual(['Squat', 'Lunge'])
})
