import { expect, it } from 'vitest'
import { RANGES, withinRange } from './range'

const now = Date.UTC(2026, 5, 30)
const daysAgo = (n: number) => now - n * 86_400_000

it('keeps only items inside the range', () => {
  const items = [daysAgo(5), daysAgo(29), daysAgo(31), daysAgo(400)]
  expect(withinRange(items, RANGES[0], (x) => x, now)).toEqual([daysAgo(5), daysAgo(29)])
})

it('all time keeps everything', () => {
  const items = [daysAgo(5), daysAgo(4000)]
  expect(withinRange(items, RANGES[3], (x) => x, now)).toEqual(items)
})
