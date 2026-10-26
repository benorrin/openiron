import { Link, useParams } from 'react-router-dom'
import { useCommunityProfile } from '../api/hooks'
import { BarChart } from '../components/charts/BarChart'
import { formatDate, formatKg, formatShortDate, formatVolume } from '../lib/format'
import { PageSkeleton } from '../components/Skeleton'
import { Icon } from '../components/Icon'

// Bodyweight exercises are logged at 0 kg, which would read as a failed lift.
const lift = (kg: number) => (kg > 0 ? `${formatKg(kg)} kg` : '–')

export function CommunityProfilePage() {
  const { username } = useParams()
  const { data: profile, isLoading, error } = useCommunityProfile(username)

  if (isLoading) return <PageSkeleton />
  if (!profile)
    return (
      <div className="page">
        <p className="error">{error?.message ?? 'Member not found.'}</p>
        <Link to="/community">Back to community</Link>
      </div>
    )

  const { member } = profile

  return (
    <div className="page">
      <div className="page-header">
        <h1>
          {member.username} {member.isYou && <span className="pill">You</span>}
        </h1>
        <Link to="/community" className="btn ghost">
          Back
        </Link>
      </div>

      <div className="tiles">
        <div className="card tile">
          <span className="icon-chip"><Icon name="calendar" size={18} /></span>
          <div className="tile-label">Workouts, last 30 days</div>
          <div className="tile-value">{member.workoutsLast30Days}</div>
          <div className="tile-sub">{member.totalWorkouts} in total</div>
        </div>
        <div className="card tile">
          <span className="icon-chip"><Icon name="dumbbell" size={18} /></span>
          <div className="tile-label">Volume, last 30 days</div>
          <div className="tile-value">{formatVolume(member.volumeLast30DaysKg)} kg</div>
          <div className="tile-sub">{member.lastWorkoutAt ? `Last trained ${formatDate(member.lastWorkoutAt)}` : 'No workouts yet'}</div>
        </div>
      </div>

      <section className="card">
        <div className="card-title">
          <h2>Weekly volume</h2>
          <span className="muted">kg lifted, last 12 weeks</span>
        </div>
        <BarChart
          ariaLabel="Weekly volume"
          unit="kg"
          color="var(--series-1)"
          bars={profile.weeks.map((w) => ({
            key: w.weekStart,
            label: formatShortDate(w.weekStart),
            title: `Week of ${formatDate(w.weekStart)} · ${w.workouts} ${w.workouts === 1 ? 'workout' : 'workouts'}`,
            value: w.volumeKg,
          }))}
        />
      </section>

      <div className="two-col">
        <section className="card">
          <h2 className="card-title">Main lifts</h2>
          {profile.topLifts.length === 0 ? (
            <p className="muted">No lifts logged yet.</p>
          ) : (
            <table className="chart-table">
              <thead>
                <tr>
                  <th scope="col">Exercise</th>
                  <th scope="col">Heaviest</th>
                  <th scope="col">Est. 1RM</th>
                </tr>
              </thead>
              <tbody>
                {profile.topLifts.map((l) => (
                  <tr key={l.exerciseName}>
                    <td>
                      {l.exerciseName}
                      <span className="muted" style={{ display: 'block' }}>
                        {l.sessions} {l.sessions === 1 ? 'session' : 'sessions'}
                      </span>
                    </td>
                    <td>{lift(l.heaviestWeightKg)}</td>
                    <td>{lift(l.best1RmKg)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </section>

        <section className="card">
          <h2 className="card-title">Recent workouts</h2>
          {profile.recentWorkouts.length === 0 ? (
            <p className="muted">Nothing logged yet.</p>
          ) : (
            <ul className="list">
              {profile.recentWorkouts.map((w, i) => (
                <li key={i} className="row" style={{ padding: '10px 4px', justifyContent: 'space-between', flexWrap: 'nowrap' }}>
                  <div>
                    <strong>{w.name}</strong>
                    <p className="muted">
                      {formatDate(w.performedAt)} · {w.setCount} sets
                    </p>
                  </div>
                  <span className="pill">{formatVolume(w.totalVolumeKg)} kg</span>
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>
    </div>
  )
}
