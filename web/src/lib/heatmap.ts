import type { HeatCell } from '../api/consistency'
import { parseDate } from './format'

export interface HeatDay {
  date: string
  level: number
  cell: HeatCell | null
}

export interface HeatGrid {
  /** One array per week, Monday first. A day after today is null so the last column stops where the data does. */
  weeks: (HeatDay | null)[][]
  months: { week: number; label: string }[]
}

const iso = (d: Date) =>
  `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`

const monthName = new Intl.DateTimeFormat(undefined, { month: 'short' })

/**
 * Lays days out the way a contribution graph does: a column per week, a row per weekday. `start` is a Monday and
 * `today` the last day to draw. A month label sits above the first week that begins in it.
 */
export function buildHeatGrid(start: string, today: string, cells: HeatCell[]): HeatGrid {
  const byDate = new Map(cells.map((c) => [c.date, c]))
  const first = parseDate(start)
  const last = parseDate(today)

  const weeks: (HeatDay | null)[][] = []
  const months: { week: number; label: string }[] = []
  let lastMonth = -1

  for (let weekStart = new Date(first); weekStart <= last; weekStart.setDate(weekStart.getDate() + 7)) {
    const week: (HeatDay | null)[] = []
    for (let i = 0; i < 7; i++) {
      const day = new Date(weekStart)
      day.setDate(day.getDate() + i)
      if (day > last) {
        week.push(null)
        continue
      }
      const key = iso(day)
      const cell = byDate.get(key) ?? null
      week.push({ date: key, level: cell?.level ?? 0, cell })
    }

    // Label a month at the first column that starts in it. Labels closer than three columns would overlap, so
    // the later one is dropped, except that a sliver of a month at the very left gives way to the next month.
    const month = weekStart.getMonth()
    const index = weeks.length
    if (month !== lastMonth) {
      const label = { week: index, label: monthName.format(weekStart) }
      const previous = months[months.length - 1]
      if (!previous || index - previous.week >= 3) months.push(label)
      else if (previous.week === 0) months[months.length - 1] = label
      lastMonth = month
    }
    weeks.push(week)
  }

  return { weeks, months }
}

export interface MonthTotal {
  label: string
  activeDays: number
}

/** Active days per calendar month, newest first: the table behind the heatmap. */
export function monthlyActiveDays(cells: HeatCell[]): MonthTotal[] {
  const totals = new Map<string, number>()
  for (const c of cells) {
    if (c.level === 0) continue
    const key = c.date.slice(0, 7)
    totals.set(key, (totals.get(key) ?? 0) + 1)
  }
  const formatter = new Intl.DateTimeFormat(undefined, { month: 'long', year: 'numeric' })
  return [...totals.entries()]
    .sort(([a], [b]) => b.localeCompare(a))
    .map(([key, activeDays]) => ({ label: formatter.format(parseDate(`${key}-01`)), activeDays }))
}

export function describeCell(cell: HeatCell | null, stepGoal: number): { title: string; rows: { label: string; value: string }[] } {
  if (!cell || (cell.level === 0 && !cell.steps)) return { title: '', rows: [{ label: 'No activity', value: '–' }] }

  const rows: { label: string; value: string }[] = []
  if (cell.workouts > 0) rows.push({ label: cell.workouts === 1 ? 'workout' : 'workouts', value: String(cell.workouts) })
  if (cell.activities > 0) {
    rows.push({ label: cell.activities === 1 ? 'activity' : 'activities', value: `${cell.activities} · ${cell.minutes} min` })
  }
  if (cell.steps) {
    rows.push({ label: cell.steps >= stepGoal ? 'steps (goal met)' : 'steps', value: cell.steps.toLocaleString() })
  }
  return { title: '', rows }
}
