import { describe, expect, it } from 'vitest'
import type { HeatCell } from '../api/consistency'
import { buildHeatGrid, describeCell, monthlyActiveDays } from './heatmap'

const cell = (date: string, patch: Partial<HeatCell> = {}): HeatCell => ({
  date,
  level: 1,
  workouts: 1,
  activities: 0,
  minutes: 0,
  steps: null,
  ...patch,
})

describe('buildHeatGrid', () => {
  // Monday 2026-06-01 to Wednesday 2026-06-17: three weeks, the last only partly filled.
  const grid = buildHeatGrid('2026-06-01', '2026-06-17', [cell('2026-06-03', { level: 2 }), cell('2026-06-17', { level: 4 })])

  it('has a column per week and seven rows each', () => {
    expect(grid.weeks).toHaveLength(3)
    expect(grid.weeks.every((w) => w.length === 7)).toBe(true)
  })

  it('starts each week on Monday', () => {
    expect(grid.weeks[0][0]?.date).toBe('2026-06-01')
    expect(grid.weeks[1][0]?.date).toBe('2026-06-08')
  })

  it('leaves days after today empty', () => {
    expect(grid.weeks[2][2]?.date).toBe('2026-06-17') // Wednesday
    expect(grid.weeks[2].slice(3)).toEqual([null, null, null, null])
  })

  it('carries levels from the data and zero elsewhere', () => {
    expect(grid.weeks[0][2]?.level).toBe(2)
    expect(grid.weeks[2][2]?.level).toBe(4)
    expect(grid.weeks[0][0]?.level).toBe(0)
  })

  it('labels the month at the first week that starts in it', () => {
    expect(grid.months[0]).toMatchObject({ week: 0 })
  })

  it('lets the next month take the label when the first is only a sliver', () => {
    // Monday 27 Oct 2025: October has one column before November starts.
    const year = buildHeatGrid('2025-10-27', '2026-10-26', [])
    expect(year.months[0]).toMatchObject({ week: 1 })
    expect(year.months[0].label).toBe(new Intl.DateTimeFormat(undefined, { month: 'short' }).format(new Date(2025, 10, 3)))
  })

  it('does not crowd month labels', () => {
    const year = buildHeatGrid('2025-07-14', '2026-07-08', [])
    const gaps = year.months.slice(1).map((m, i) => m.week - year.months[i].week)
    expect(gaps.every((g) => g >= 3)).toBe(true)
    expect(year.weeks).toHaveLength(52)
  })
})

describe('monthlyActiveDays', () => {
  it('counts active days per month, newest first, and ignores level-0 days', () => {
    const totals = monthlyActiveDays([
      cell('2026-05-02'),
      cell('2026-05-09'),
      cell('2026-06-01'),
      cell('2026-06-02', { level: 0, steps: 3000 }),
    ])
    expect(totals.map((t) => t.activeDays)).toEqual([1, 2])
  })
})

describe('describeCell', () => {
  it('summarises a busy day', () => {
    const { rows } = describeCell(cell('2026-06-03', { workouts: 1, activities: 2, minutes: 75, steps: 9500 }), 8000)
    expect(rows).toEqual([
      { label: 'workout', value: '1' },
      { label: 'activities', value: '2 · 75 min' },
      { label: 'steps (goal met)', value: (9500).toLocaleString() },
    ])
  })

  it('shows steps that fell short of the goal', () => {
    const { rows } = describeCell(cell('2026-06-03', { level: 0, workouts: 0, steps: 3000 }), 8000)
    expect(rows).toEqual([{ label: 'steps', value: (3000).toLocaleString() }])
  })

  it('says so when nothing happened', () => {
    expect(describeCell(null, 8000).rows[0].label).toBe('No activity')
  })
})
