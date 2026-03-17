package models

import (
	"database/sql/driver"
	"encoding/json"
	"time"
)

type Workout struct {
	ID        int       `db:"id" json:"id"`
	UserID    int       `db:"user_id" json:"user_id"`
	Date      time.Time `db:"date" json:"date"`
	Notes     *string   `db:"notes" json:"notes"`
	CreatedAt time.Time `db:"created_at" json:"created_at"`
	UpdatedAt time.Time `db:"updated_at" json:"updated_at"`
}

type WorkoutExercise struct {
	ID                int             `db:"id" json:"id"`
	WorkoutID         int             `db:"workout_id" json:"workout_id"`
	ExerciseID        int             `db:"exercise_id" json:"exercise_id"`
	ExerciseLogData   ExerciseLogData `db:"exercise_log_data" json:"exercise_log_data"`
	Notes             *string         `db:"notes" json:"notes"`
	CreatedAt         time.Time       `db:"created_at" json:"created_at"`
	UpdatedAt         time.Time       `db:"updated_at" json:"updated_at"`
}

// ExerciseLogData supports both strength and cardio exercises
type ExerciseLogData struct {
	Type string          `json:"type"` // "strength" or "cardio"
	Data json.RawMessage `json:"data"` // Flexible data structure
}

// Scanning and value methods for PostgreSQL JSONB
func (e *ExerciseLogData) Scan(value interface{}) error {
	if value == nil {
		return nil
	}
	bytes, ok := value.([]byte)
	if !ok {
		return nil
	}
	return json.Unmarshal(bytes, &e)
}

func (e ExerciseLogData) Value() (driver.Value, error) {
	return json.Marshal(e)
}

// Strength exercise log structure
type StrengthLog struct {
	Type string      `json:"type"`
	Sets []SetLog    `json:"sets"`
}

type SetLog struct {
	Reps   int     `json:"reps"`
	Weight float64 `json:"weight"`
	Unit   string  `json:"unit"` // "lbs" or "kg"
}

// Cardio exercise log structure
type CardioLog struct {
	Type         string  `json:"type"`
	Distance     float64 `json:"distance"`
	DistanceUnit string  `json:"distance_unit"` // "km" or "mi"
	TimeMinutes  float64 `json:"time_minutes"`
	AvgPace      string  `json:"avg_pace"`
}
