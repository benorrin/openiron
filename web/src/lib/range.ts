export interface Range {
  label: string
  /** Days back from now; null means everything. */
  days: number | null
}

export const RANGES: Range[] = [
  { label: '30 days', days: 30 },
  { label: '90 days', days: 90 },
  { label: '1 year', days: 365 },
  { label: 'All time', days: null },
]

/** Keeps items dated on or after the range start. */
export function withinRange<T>(items: T[], range: Range, dateOf: (item: T) => number, now = Date.now()): T[] {
  if (range.days === null) return items
  const cutoff = now - range.days * 86_400_000
  return items.filter((item) => dateOf(item) >= cutoff)
}
