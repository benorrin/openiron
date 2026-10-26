import { describe, expect, it } from 'vitest'
import { formatKg, formatVolume, fromLocalInput, parseDate, toLocalInput } from './format'

describe('formatKg', () => {
  it('drops trailing zeros and rounds to one decimal', () => {
    expect(formatKg(100)).toBe('100')
    expect(formatKg(82.5)).toBe('82.5')
    expect(formatKg(82.26)).toBe('82.3')
  })
})

describe('formatVolume', () => {
  it('compacts from 1,000 up', () => {
    expect(formatVolume(950)).toBe('950')
    expect(formatVolume(12400)).toBe('12.4K')
    expect(formatVolume(1000)).toBe('1K')
  })
})

describe('dates', () => {
  it('reads date-only values as local midnight, not UTC', () => {
    const d = parseDate('2026-03-05')
    expect([d.getFullYear(), d.getMonth(), d.getDate()]).toEqual([2026, 2, 5])
  })

  it('round-trips datetime-local input values', () => {
    const local = '2026-03-05T18:30'
    expect(toLocalInput(fromLocalInput(local))).toBe(local)
  })
})
