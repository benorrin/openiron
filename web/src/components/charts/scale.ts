export interface Ticks {
  ticks: number[]
  min: number
  max: number
}

function niceStep(range: number, target: number): number {
  const raw = range / target
  const magnitude = 10 ** Math.floor(Math.log10(raw))
  const n = raw / magnitude
  const multiple = n < 1.5 ? 1 : n < 3 ? 2 : n < 7 ? 5 : 10
  return multiple * magnitude
}

/** Round axis bounds outwards to clean numbers, returning roughly `target` evenly spaced ticks. */
export function niceTicks(min: number, max: number, target = 5): Ticks {
  if (min === max) {
    const pad = Math.max(Math.abs(min) * 0.1, 1)
    min -= pad
    max += pad
  }
  const step = niceStep(max - min, target)
  const start = Math.floor(min / step) * step
  const end = Math.ceil(max / step) * step
  const ticks: number[] = []
  // Index-based so floating point error doesn't accumulate across steps.
  for (let i = 0; start + i * step <= end + step / 1e6; i++) ticks.push(Math.round((start + i * step) * 1e6) / 1e6)
  return { ticks, min: start, max: end }
}

export function linear(domain: [number, number], range: [number, number]): (v: number) => number {
  const [d0, d1] = domain
  const [r0, r1] = range
  if (d0 === d1) return () => (r0 + r1) / 2
  return (v) => r0 + ((v - d0) / (d1 - d0)) * (r1 - r0)
}

/** Evenly spaced values across a range, used for time axis ticks. */
export function evenTicks(min: number, max: number, count: number): number[] {
  if (count < 2 || min === max) return [min]
  return Array.from({ length: count }, (_, i) => min + ((max - min) * i) / (count - 1))
}

/** Index of the value in a sorted array closest to `target`. */
export function nearestIndex(sorted: number[], target: number): number {
  let best = 0
  for (let i = 1; i < sorted.length; i++) {
    if (Math.abs(sorted[i] - target) < Math.abs(sorted[best] - target)) best = i
  }
  return best
}

/** Axis labels: whole numbers stay plain, thousands compact (1,200 -> 1.2K). */
export function formatTick(v: number): string {
  if (Math.abs(v) >= 1000) return `${Math.round(v / 100) / 10}K`
  return String(Math.round(v * 10) / 10)
}
