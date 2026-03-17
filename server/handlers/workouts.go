package handlers

import (
	"github.com/gin-gonic/gin"
)

// CreateWorkout creates a new workout session
func CreateWorkout(c *gin.Context) {
	// POST /api/v1/workouts
}

// ListWorkouts retrieves user's workout history
func ListWorkouts(c *gin.Context) {
	// GET /api/v1/workouts
}

// GetWorkout retrieves a specific workout with all exercises
func GetWorkout(c *gin.Context) {
	// GET /api/v1/workouts/:id
}

// UpdateWorkout modifies an existing workout
func UpdateWorkout(c *gin.Context) {
	// PUT /api/v1/workouts/:id
}

// DeleteWorkout removes a workout from the database
func DeleteWorkout(c *gin.Context) {
	// DELETE /api/v1/workouts/:id
}

// AddExerciseToWorkout adds an exercise to an existing workout
func AddExerciseToWorkout(c *gin.Context) {
	// POST /api/v1/workouts/:id/exercises
}

func UpdateWorkoutExercise(c *gin.Context) {
	// PUT /api/v1/workouts/:id/exercises/:exerciseId
}

func RemoveExerciseFromWorkout(c *gin.Context) {
	// DELETE /api/v1/workouts/:id/exercises/:exerciseId
}
