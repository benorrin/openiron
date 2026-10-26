// Mirrors the DTOs in api/src/OpenIron.Api/Contracts.

export interface User {
  id: string
  username: string
}

export interface AuthResponse {
  token: string
  expiresAt: string
  user: User
}

export interface Exercise {
  id: string
  name: string
  muscleGroup: string
  isCustom: boolean
}

export interface TemplateExercise {
  exerciseId: string
  exerciseName: string
  muscleGroup: string
  targetSets: number
  targetReps: number
}

export interface Template {
  id: string
  name: string
  notes: string | null
  exercises: TemplateExercise[]
}

export interface TemplateInput {
  name: string
  notes: string | null
  exercises: { exerciseId: string; targetSets: number; targetReps: number }[]
}

export interface WorkoutSet {
  reps: number
  weightKg: number
}

export interface WorkoutExercise {
  exerciseId: string
  exerciseName: string
  muscleGroup: string
  sets: WorkoutSet[]
}

export interface Workout {
  id: string
  templateId: string | null
  name: string
  performedAt: string
  durationMinutes: number | null
  notes: string | null
  totalVolumeKg: number
  exercises: WorkoutExercise[]
}

export interface WorkoutSummary {
  id: string
  name: string
  performedAt: string
  durationMinutes: number | null
  exerciseCount: number
  setCount: number
  totalVolumeKg: number
}

export interface WorkoutInput {
  name: string
  performedAt: string | null
  durationMinutes: number | null
  notes: string | null
  templateId: string | null
  exercises: { exerciseId: string; sets: WorkoutSet[] }[]
}

export interface WorkoutDraft {
  templateId: string
  name: string
  exercises: WorkoutExercise[]
}

export interface BodyWeightEntry {
  id: string
  date: string
  weightKg: number
}

export interface ProgressExercise {
  exerciseId: string
  name: string
  muscleGroup: string
  sessions: number
  lastPerformedAt: string
}

export interface ExercisePoint {
  workoutId: string
  performedAt: string
  topWeightKg: number
  estimated1RmKg: number
  volumeKg: number
  totalReps: number
}

export interface ExerciseProgress {
  exerciseId: string
  name: string
  muscleGroup: string
  records: {
    heaviestWeightKg: number
    heaviestOn: string | null
    best1RmKg: number
    best1RmOn: string | null
  }
  points: ExercisePoint[]
}

export interface WeeklyActivity {
  weekStart: string
  workouts: number
  volumeKg: number
}

export interface Overview {
  totalWorkouts: number
  workoutsLast30Days: number
  latestBodyWeightKg: number | null
  bodyWeightChangeKg: number | null
  weeks: WeeklyActivity[]
}

export interface CommunityMember {
  username: string
  isYou: boolean
  totalWorkouts: number
  workoutsLast30Days: number
  volumeLast30DaysKg: number
  lastWorkoutAt: string | null
}

export interface TopLift {
  exerciseName: string
  muscleGroup: string
  heaviestWeightKg: number
  best1RmKg: number
  sessions: number
}

export interface RecentWorkout {
  name: string
  performedAt: string
  exerciseCount: number
  setCount: number
  totalVolumeKg: number
}

export interface CommunityProfile {
  member: CommunityMember
  weeks: WeeklyActivity[]
  topLifts: TopLift[]
  recentWorkouts: RecentWorkout[]
}
