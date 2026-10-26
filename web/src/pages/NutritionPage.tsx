import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { useBodyWeight } from '../api/hooks'
import { useApplyTarget, useClearApplied, useNutrition, type Nutrition } from '../api/nutrition'
import { BodyTabs } from '../components/BodyTabs'
import { LineChart } from '../components/charts/LineChart'
import { EmptyState } from '../components/EmptyState'
import { Icon } from '../components/Icon'
import { MacroBar } from '../components/MacroBar'
import { NutritionForm } from '../components/NutritionForm'
import { PageSkeleton } from '../components/Skeleton'
import { formatDate, formatKg, formatShortDate, parseDate } from '../lib/format'
import { describeProgress, describeRate, formatSigned, type Tone } from '../lib/nutrition'

export function NutritionPage() {
  const { data, isLoading, error } = useNutrition()

  if (isLoading) return <PageSkeleton />
  if (!data) {
    return (
      <div className="page">
        <BodyTabs />
        <p className="error">{error?.message ?? 'Something went wrong.'}</p>
      </div>
    )
  }

  return (
    <div className="page">
      <BodyTabs />
      <h1>Nutrition</h1>
      {data.settings === null ? (
        <section className="card stack">
          <div>
            <h2>Set up your calorie plan</h2>
            <p className="muted" style={{ marginTop: 4 }}>
              Tell us about you and your goal. We'll work out a daily target, then use your weigh-ins to check it is
              working and adjust it if it isn't.
            </p>
          </div>
          <NutritionForm initial={null} submitLabel="Calculate my target" />
        </section>
      ) : (
        <PlanView data={data} />
      )}
    </div>
  )
}

function PlanView({ data }: { data: Nutrition }) {
  const { settings, plan } = data
  if (!settings) return null

  return (
    <>
      {plan ? (
        <>
          <TargetCard data={data} />
          <FollowingCard data={data} />
          <ProgressCard data={data} />
          <TrendCard data={data} />
          <HowItWorks data={data} />
        </>
      ) : (
        <EmptyState
          icon="scale"
          title="Log a weigh-in to get your target"
          action={
            <Link to="/body-weight" className="btn primary">
              Log your weight
            </Link>
          }
        >
          Your calories depend on your current weight, and your weigh-ins are how we check the plan is working.
        </EmptyState>
      )}
      <EditSettings data={data} />
    </>
  )
}

function goalSummary(data: Nutrition): string {
  const s = data.settings!
  if (s.goal === 'maintain') return 'Maintain your weight'
  const rate = data.plan ? Math.abs(data.plan.progress.plannedRateKgPerWeek) : s.weeklyRateKg
  return `${s.goal === 'lose' ? 'Lose' : 'Gain'} ${rate} kg a week to ${formatKg(s.targetWeightKg ?? 0)} kg`
}

function TargetCard({ data }: { data: Nutrition }) {
  const plan = data.plan!

  return (
    <section className="card stack">
      <div className="row" style={{ justifyContent: 'space-between', alignItems: 'flex-end' }}>
        <div>
          <p className="muted">Daily target</p>
          <div className="target-number">{plan.targetKcal.toLocaleString()}</div>
          <p className="muted" style={{ marginTop: 6 }}>
            calories a day · based on {formatKg(plan.currentWeightKg)} kg
          </p>
        </div>
        <span className="pill">{goalSummary(data)}</span>
      </div>
      <MacroBar macros={plan.macros} />
      {plan.limitedBySafetyFloor && (
        <div className="callout warn">
          <span>
            <strong>This is the lowest target we'll suggest.</strong> Your goal needs less than that, so choose a slower
            pace or a later date rather than eating less.
          </span>
        </div>
      )}
      {plan.rateLimited && (
        <p className="muted">
          Your chosen pace was reduced to {Math.abs(plan.progress.plannedRateKgPerWeek)} kg a week, the most we recommend
          at your weight.
        </p>
      )}
    </section>
  )
}

/** What the user is currently aiming for, which is what the weigh-ins are measured against. */
function FollowingCard({ data }: { data: Nutrition }) {
  const settings = data.settings!
  const plan = data.plan!
  const apply = useApplyTarget()
  const clear = useClearApplied()
  const [custom, setCustom] = useState('')
  const [open, setOpen] = useState(false)
  const error = apply.error ?? clear.error

  async function applyCustom(e: FormEvent) {
    e.preventDefault()
    try {
      await apply.mutateAsync(Number(custom))
      setCustom('')
      setOpen(false)
    } catch {
      // Shown from apply.error.
    }
  }

  return (
    <section className="card stack">
      {settings.appliedKcal === null ? (
        <>
          <div>
            <h2>Start following it</h2>
            <p className="muted" style={{ marginTop: 4 }}>
              We don't track what you eat, so tell us the number you're aiming for. Then keep weighing in: after about
              three weeks we compare how your weight responds and fine-tune the target.
            </p>
          </div>
          <div className="row">
            <button className="btn primary" disabled={apply.isPending} onClick={() => apply.mutate(undefined)}>
              <Icon name="check" size={18} />
              Follow {plan.targetKcal.toLocaleString()} kcal
            </button>
            <button className="btn" onClick={() => setOpen((v) => !v)}>
              I eat something different
            </button>
          </div>
        </>
      ) : (
        <>
          <div className="row" style={{ justifyContent: 'space-between' }}>
            <div>
              <h2>Following {settings.appliedKcal.toLocaleString()} kcal</h2>
              <p className="muted">
                since {settings.appliedOn ? formatDate(settings.appliedOn) : 'recently'}. The check assumes this is what
                you've been eating.
              </p>
            </div>
            <div className="row">
              <button className="btn small" onClick={() => setOpen((v) => !v)}>
                Change
              </button>
              <button className="btn small ghost" disabled={clear.isPending} onClick={() => clear.mutate()}>
                Stop tracking
              </button>
            </div>
          </div>
        </>
      )}
      {open && (
        <form className="row" onSubmit={applyCustom} style={{ alignItems: 'flex-end' }}>
          <label className="field grow">
            Calories I actually eat on average
            <input type="number" inputMode="numeric" min={800} max={8000} value={custom} onChange={(e) => setCustom(e.target.value)} required />
          </label>
          <button className="btn primary" disabled={apply.isPending}>
            Use this
          </button>
        </form>
      )}
      {error && <p className="error" role="alert">{error.message}</p>}
    </section>
  )
}

const TONE_ICON = { good: 'check', warn: 'alert', neutral: 'info' } as const

function ProgressCard({ data }: { data: Nutrition }) {
  const plan = data.plan!
  const settings = data.settings!
  const progress = plan.progress
  const apply = useApplyTarget()
  const message = describeProgress(progress, settings.goal)
  const tone: Tone = message.tone

  return (
    <section className="card stack">
      <div className="status">
        <span className={`icon-chip ${tone}`}>
          <Icon name={TONE_ICON[tone]} />
        </span>
        <div>
          <h2>{message.title}</h2>
          <p className="muted" style={{ marginTop: 2 }}>{message.detail}</p>
        </div>
      </div>

      {progress.suggestedAdjustmentKcal !== null && (
        <div className={`callout ${tone}`}>
          <span>
            <strong>Suggested change: {formatSigned(progress.suggestedAdjustmentKcal)} kcal</strong>
            <span className="muted" style={{ display: 'block' }}>
              Aim for {plan.targetKcal.toLocaleString()} kcal a day instead.
            </span>
          </span>
          <button className="btn primary small" disabled={apply.isPending} onClick={() => apply.mutate(undefined)}>
            Switch to {plan.targetKcal.toLocaleString()}
          </button>
        </div>
      )}
      {apply.error && <p className="error" role="alert">{apply.error.message}</p>}

      <dl className="facts">
        <div>
          <dt>Measured pace</dt>
          <dd>{progress.observedRateKgPerWeek === null ? '–' : describeRate(progress.observedRateKgPerWeek).replace(' a week', '/wk')}</dd>
        </div>
        <div>
          <dt>Planned pace</dt>
          <dd>{describeRate(progress.plannedRateKgPerWeek).replace(' a week', '/wk')}</dd>
        </div>
        {progress.remainingKg !== null && (
          <div>
            <dt>{progress.goalReached ? 'Goal' : 'To go'}</dt>
            <dd>{progress.goalReached ? 'Reached' : `${formatKg(Math.abs(progress.remainingKg))} kg`}</dd>
          </div>
        )}
        {progress.estimatedGoalDate && !progress.goalReached && (
          <div>
            <dt>Estimated date</dt>
            <dd>{formatDate(progress.estimatedGoalDate)}</dd>
          </div>
        )}
      </dl>
    </section>
  )
}

function TrendCard({ data }: { data: Nutrition }) {
  const plan = data.plan!
  const { data: entries = [] } = useBodyWeight()
  const first = plan.trend[0]?.date
  const weighIns = entries.filter((e) => !first || e.date >= first)
  const x = (date: string) => parseDate(date).getTime()

  if (plan.trend.length < 2) return null

  return (
    <section className="card">
      <div className="card-title">
        <h2>Weight and trend</h2>
        <span className="muted">kg, last 90 days</span>
      </div>
      <LineChart
        ariaLabel="Body weight and smoothed trend"
        unit="kg"
        formatX={(t) => formatShortDate(new Date(t).toISOString())}
        series={[
          { id: 'trend', label: 'Trend', color: 'var(--series-1)', points: plan.trend.map((p) => ({ x: x(p.date), y: p.weightKg })) },
          { id: 'weighins', label: 'Weigh-ins', color: 'var(--series-2)', points: weighIns.map((e) => ({ x: x(e.date), y: e.weightKg })) },
        ]}
      />
      <p className="muted" style={{ marginTop: 8 }}>
        Day-to-day weight swings with water and food. The trend smooths that out, and it's what your calories are based on.
      </p>
    </section>
  )
}

function HowItWorks({ data }: { data: Nutrition }) {
  const plan = data.plan!
  const applied = data.settings!.appliedKcal

  return (
    <details className="card disclosure">
      <summary>How this is worked out</summary>
      <div className="stack">
        <dl className="facts">
          <div>
            <dt>Resting burn (BMR)</dt>
            <dd>{plan.bmrKcal.toLocaleString()} kcal</dd>
          </div>
          <div>
            <dt>Maintenance by formula</dt>
            <dd>{plan.formulaMaintenanceKcal.toLocaleString()} kcal</dd>
          </div>
          <div>
            <dt>Maintenance we estimate</dt>
            <dd>{plan.estimatedMaintenanceKcal.toLocaleString()} kcal</dd>
          </div>
          <div>
            <dt>Trust in your data</dt>
            <dd>{Math.round(plan.confidence * 100)}%</dd>
          </div>
        </dl>
        <p className="muted">
          We start with the Mifflin-St Jeor formula for your resting burn and scale it by your activity level. A pound of
          body weight is roughly 3,500 calories (7,700 per kg), so each half kilo a week you want to change means about 550
          calories a day above or below maintenance.
        </p>
        <p className="muted">
          Once you've followed a target for a few weeks, your weigh-ins tell us what your body really does.
          {applied !== null
            ? ` If you ate about ${applied.toLocaleString()} kcal a day and your weight moved by a known amount, your real maintenance is whatever makes those numbers add up.`
            : ' Apply a target to start that comparison.'}{' '}
          The more weigh-ins and the longer the stretch, the more that figure outweighs the formula.
        </p>
        <p className="muted">
          It only works if the number you follow is close to what you actually eat, so correct it if you've drifted. It
          never goes below a safety floor, and weight loss is capped at what is realistic for your size.
        </p>
      </div>
    </details>
  )
}

function EditSettings({ data }: { data: Nutrition }) {
  const [open, setOpen] = useState(false)
  return (
    <details className="card disclosure" open={open} onToggle={(e) => setOpen(e.currentTarget.open)}>
      <summary>Edit profile and goal</summary>
      <NutritionForm initial={data.settings} submitLabel="Save changes" onSaved={() => setOpen(false)} />
    </details>
  )
}
