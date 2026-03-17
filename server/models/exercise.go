package models

import (
	"database/sql/driver"
	"encoding/json"
	"time"
)

type ExerciseType string

const (
	ExerciseTypeStrength    ExerciseType = "strength"
	ExerciseTypeCardio      ExerciseType = "cardio"
	ExerciseTypeFlexibility ExerciseType = "flexibility"
)

// Common muscle groups for exercises
var MuscleGroups = []string{
	"chest", "back", "shoulders", "biceps", "triceps", "forearms",
	"quadriceps", "hamstrings", "calves", "glutes", "abdominals", "obliques",
	"traps", "lats", "rhomboids", "deltoids", "pectorals",
}

// MuscleGroupList represents a list of target muscle groups for an exercise
type MuscleGroupList []string

// Scan implements the sql.Scanner interface for JSONB storage
func (m *MuscleGroupList) Scan(value interface{}) error {
	if value == nil {
		*m = MuscleGroupList{}
		return nil
	}

	bytes, ok := value.([]byte)
	if !ok {
		*m = MuscleGroupList{}
		return nil
	}

	return json.Unmarshal(bytes, m)
}

// Value implements the driver.Valuer interface for JSONB storage
func (m MuscleGroupList) Value() (driver.Value, error) {
	if len(m) == 0 {
		return nil, nil
	}
	return json.Marshal(m)
}

type Exercise struct {
	ID            int             `db:"id" json:"id"`
	Name          string          `db:"name" json:"name"`
	Type          ExerciseType    `db:"type" json:"type"`
	TargetMuscles MuscleGroupList `db:"target_muscles" json:"target_muscles"`
	Description   *string         `db:"description" json:"description"`
	CreatedBy     string          `db:"created_by" json:"created_by"` // "system" or "user:{user_id}"
	CreatedAt     time.Time       `db:"created_at" json:"created_at"`
}
