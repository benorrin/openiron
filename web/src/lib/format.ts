const dateFormat = new Intl.DateTimeFormat(undefined, { day: 'numeric', month: 'short', year: 'numeric' })
const shortDateFormat = new Intl.DateTimeFormat(undefined, { day: 'numeric', month: 'short' })

/** Dates from the API arrive as ISO strings; date-only values (YYYY-MM-DD) are shown without timezone shifts. */
export function parseDate(value: string): Date {
  return /^\d{4}-\d{2}-\d{2}$/.test(value) ? new Date(`${value}T00:00:00`) : new Date(value)
}

export const formatDate = (value: string) => dateFormat.format(parseDate(value))
export const formatShortDate = (value: string) => shortDateFormat.format(parseDate(value))

/** 100 -> "100", 82.5 -> "82.5", 82.25 -> "82.3" */
export function formatKg(kg: number): string {
  return String(Math.round(kg * 10) / 10)
}

/** Compact large totals: 950 -> "950", 12400 -> "12.4K". */
export function formatVolume(kg: number): string {
  if (kg < 1000) return `${Math.round(kg)}`
  return `${Math.round(kg / 100) / 10}K`
}

/** The value an <input type="datetime-local"> expects, in the user's local time. */
export function toLocalInput(iso: string): string {
  const d = new Date(iso)
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}

export function fromLocalInput(local: string): string {
  return new Date(local).toISOString()
}

export function todayIso(): string {
  const d = new Date()
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}
