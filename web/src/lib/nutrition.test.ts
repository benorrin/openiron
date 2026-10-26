import { describe, expect, it } from 'vitest'
import type { PlanProgress } from '../api/nutrition'
import { describeProgress, describeRate, formatSigned, macroShares } from './nutrition'

const progress = (patch: Partial<PlanProgress>): PlanProgress => ({
  status: 'onTrack',
  observedRateKgPerWeek: -0.5,
  plannedRateKgPerWeek: -0.5,
  weighInsUsed: 12,
  daysOfData: 20,
  daysUntilCheck: null,
  suggestedAdjustmentKcal: null,
  remainingKg: null,
  goalReached: false,
  estimatedGoalDate: null,
  ...patch,
})

describe('describeRate', () => {
  it('uses words for direction', () => {
    expect(describeRate(-0.5)).toBe('losing 0.5 kg a week')
    expect(describeRate(0.25)).toBe('gaining 0.25 kg a week')
  })

  it('treats a tiny change as level', () => {
    expect(describeRate(0.02)).toBe('staying level')
    expect(describeRate(-0.04)).toBe('staying level')
  })
})

describe('describeProgress', () => {
  it('is positive only when on track', () => {
    expect(describeProgress(progress({}), 'lose').tone).toBe('good')
    for (const status of ['fasterThanPlanned', 'slowerThanPlanned', 'wrongDirection', 'driftingUp', 'driftingDown'] as const) {
      expect(describeProgress(progress({ status }), 'lose').tone).toBe('warn')
    }
  })

  it('says how long until there is enough data', () => {
    const msg = describeProgress(progress({ status: 'gathering', observedRateKgPerWeek: null, daysUntilCheck: 13, weighInsUsed: 1 }), 'lose')
    expect(msg.tone).toBe('neutral')
    expect(msg.detail).toContain('13 days')
    expect(msg.detail).toContain('1 weigh-in')
  })

  it('does not report zero weigh-ins as a count', () => {
    const msg = describeProgress(progress({ status: 'gathering', observedRateKgPerWeek: null, daysUntilCheck: 19, weighInsUsed: 0 }), 'lose')
    expect(msg.detail).toContain('No weigh-ins yet')
    expect(msg.detail).not.toContain('0 weigh-ins')
  })

  it('asks for more weigh-ins when time is not the problem', () => {
    const msg = describeProgress(progress({ status: 'gathering', observedRateKgPerWeek: null, daysUntilCheck: null, weighInsUsed: 3 }), 'lose')
    expect(msg.detail).toContain('a few more entries')
  })

  it('words the same status differently for losing and gaining', () => {
    const slowLose = describeProgress(progress({ status: 'slowerThanPlanned', observedRateKgPerWeek: -0.1 }), 'lose')
    const slowGain = describeProgress(progress({ status: 'slowerThanPlanned', observedRateKgPerWeek: 0.1, plannedRateKgPerWeek: 0.5 }), 'gain')
    expect(slowLose.title).toBe('Losing slower than planned')
    expect(slowGain.title).toBe('Gaining slower than planned')
  })
})

describe('macroShares', () => {
  it('weights fat at nine calories a gram and sums to one', () => {
    const shares = macroShares({ proteinG: 160, carbsG: 255, fatG: 61 })
    expect(shares.protein + shares.carbs + shares.fat).toBeCloseTo(1, 9)
    expect(shares.carbs).toBeGreaterThan(shares.protein)
    expect(shares.fat).toBeCloseTo((61 * 9) / (160 * 4 + 255 * 4 + 61 * 9), 9)
  })

  it('copes with nothing at all', () => {
    expect(macroShares({ proteinG: 0, carbsG: 0, fatG: 0 })).toEqual({ protein: 0, carbs: 0, fat: 0 })
  })
})

it('formatSigned uses a real minus sign', () => {
  expect(formatSigned(-110)).toBe('−110')
  expect(formatSigned(1250)).toBe('+1,250')
  expect(formatSigned(0)).toBe('0')
})
