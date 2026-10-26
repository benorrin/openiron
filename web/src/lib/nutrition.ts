import type { ActivityLevel, GoalType, Macros, PlanProgress } from '../api/nutrition'
export const ACTIVITY_OPTIONS: { value: ActivityLevel; label: string; hint: string }[] = [
  { value: 'sedentary', label: 'Sedentary', hint: 'Desk job, little exercise' },
  { value: 'light', label: 'Lightly active', hint: 'Training 1 to 3 days a week' },
  { value: 'moderate', label: 'Moderately active', hint: 'Training 3 to 5 days a week' },
  { value: 'active', label: 'Very active', hint: 'Hard training 6 to 7 days a week' },
  { value: 'veryActive', label: 'Extremely active', hint: 'Physical job plus daily training' },
]

export const RATE_OPTIONS: Record<Exclude<GoalType, 'maintain'>, number[]> = {
  lose: [0.25, 0.5, 0.75, 1],
  gain: [0.1, 0.25, 0.5],
}

export const DEFAULT_RATE: Record<GoalType, number> = { lose: 0.5, maintain: 0, gain: 0.25 }

/** "0.5 kg" with a word for the direction, since a bare minus sign is easy to misread. */
export function describeRate(kgPerWeek: number): string {
  const rounded = Math.round(Math.abs(kgPerWeek) * 100) / 100
  if (rounded < 0.05) return 'staying level'
  // Paces come in quarter-kilo steps, so unlike body weight they need two decimals.
  return `${kgPerWeek < 0 ? 'losing' : 'gaining'} ${rounded} kg a week`
}

export type Tone = 'good' | 'warn' | 'neutral'

export interface ProgressMessage {
  tone: Tone
  title: string
  detail: string
}

const plural = (n: number, word: string) => `${n} ${word}${n === 1 ? '' : 's'}`

/** Plain-language read-out of where the user stands against their plan. */
export function describeProgress(progress: PlanProgress, goal: GoalType): ProgressMessage {
  const observed = progress.observedRateKgPerWeek
  const pace = observed === null ? '' : describeRate(observed)
  const planned = describeRate(progress.plannedRateKgPerWeek)

  switch (progress.status) {
    case 'noPlan':
      return {
        tone: 'neutral',
        title: 'No target applied yet',
        detail:
          'Use the target above and weigh in regularly. After about three weeks there is enough data to see how your body is responding and to adjust.',
      }
    case 'gathering': {
      const wait =
        progress.daysUntilCheck !== null
          ? `Check back in ${plural(progress.daysUntilCheck, 'day')}.`
          : 'Keep weighing in; a few more entries are needed.'
      return {
        tone: 'neutral',
        title: 'Gathering data',
        detail:
          progress.weighInsUsed === 0
            ? `${wait} No weigh-ins yet since the target settled.`
            : `${wait} So far there ${progress.weighInsUsed === 1 ? 'is' : 'are'} ${plural(progress.weighInsUsed, 'weigh-in')} since the target settled.`,
      }
    }
    case 'onTrack':
      return {
        tone: 'good',
        title: 'On track',
        detail: `You're ${pace}, which matches the plan (${planned}). Keep going as you are.`,
      }
    case 'fasterThanPlanned':
      return {
        tone: 'warn',
        title: goal === 'gain' ? 'Gaining faster than planned' : 'Losing faster than planned',
        detail: `You're ${pace} against a plan of ${planned}. ${
          goal === 'gain' ? 'More of that is likely to be fat.' : 'That is harder to sustain and risks muscle.'
        } A few more calories would bring it back.`,
      }
    case 'slowerThanPlanned':
      return {
        tone: 'warn',
        title: goal === 'gain' ? 'Gaining slower than planned' : 'Losing slower than planned',
        detail: `You're ${pace} against a plan of ${planned}. Your body burns a little ${
          goal === 'gain' ? 'more' : 'less'
        } than the formula assumed, so the target needs adjusting.`,
      }
    case 'wrongDirection':
      return {
        tone: 'warn',
        title: 'Moving the wrong way',
        detail: `You're ${pace}, but the goal is to ${goal === 'gain' ? 'gain' : 'lose'} weight. Check the calories you have been eating against the target.`,
      }
    case 'driftingUp':
      return {
        tone: 'warn',
        title: 'Weight is creeping up',
        detail: `You're ${pace} while aiming to stay level. A small cut would hold it steady.`,
      }
    case 'driftingDown':
      return {
        tone: 'warn',
        title: 'Weight is creeping down',
        detail: `You're ${pace} while aiming to stay level. A few more calories would hold it steady.`,
      }
  }
}

/** Share of calories from each macro, for the stacked bar. Fat is 9 kcal a gram, the others 4. */
export function macroShares(macros: Macros): { protein: number; carbs: number; fat: number } {
  const protein = macros.proteinG * 4
  const carbs = macros.carbsG * 4
  const fat = macros.fatG * 9
  const total = protein + carbs + fat
  if (total === 0) return { protein: 0, carbs: 0, fat: 0 }
  return { protein: protein / total, carbs: carbs / total, fat: fat / total }
}

export function formatSigned(n: number): string {
  return `${n > 0 ? '+' : n < 0 ? '−' : ''}${Math.abs(n).toLocaleString()}`
}
