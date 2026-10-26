import { describe, expect, it } from 'vitest'
import { evenTicks, formatTick, linear, nearestIndex, niceTicks } from './scale'

describe('niceTicks', () => {
  it('rounds bounds outwards to clean steps', () => {
    const { ticks, min, max } = niceTicks(78.4, 83.1)
    expect(min).toBeLessThanOrEqual(78.4)
    expect(max).toBeGreaterThanOrEqual(83.1)
    expect(ticks[0]).toBe(min)
    expect(ticks[ticks.length - 1]).toBe(max)
    const steps = ticks.slice(1).map((t, i) => Math.round((t - ticks[i]) * 1e6) / 1e6)
    expect(new Set(steps).size).toBe(1)
  })

  it('handles a flat series without a zero-size range', () => {
    const { min, max } = niceTicks(100, 100)
    expect(max).toBeGreaterThan(min)
  })

  it('starts at zero for volume-style ranges', () => {
    expect(niceTicks(0, 8200).ticks[0]).toBe(0)
  })
})

describe('linear', () => {
  it('maps the domain onto the range, including inverted ranges', () => {
    expect(linear([0, 10], [100, 0])(2.5)).toBe(75)
  })

  it('centres everything when the domain is a single value', () => {
    expect(linear([5, 5], [0, 100])(5)).toBe(50)
  })
})

it('evenTicks spans the range inclusively', () => {
  expect(evenTicks(0, 100, 5)).toEqual([0, 25, 50, 75, 100])
  expect(evenTicks(7, 7, 5)).toEqual([7])
})

it('nearestIndex picks the closest value', () => {
  expect(nearestIndex([0, 10, 20], 13)).toBe(1)
  expect(nearestIndex([0, 10, 20], 16)).toBe(2)
})

it('formatTick compacts thousands', () => {
  expect(formatTick(250)).toBe('250')
  expect(formatTick(2500)).toBe('2.5K')
  expect(formatTick(82.5)).toBe('82.5')
})
