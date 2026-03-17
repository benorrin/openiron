package services

import (
	"fmt"

	"github.com/jmoiron/sqlx"
	"openiron-api/models"
)

// GetProfile retrieves a user's profile information
func GetProfile(db *sqlx.DB, userID int) (*models.UserProfile, error) {
	var profile models.UserProfile
	query := `SELECT id, user_id, height, measurement_unit, profile_image_path, created_at, updated_at FROM user_profiles WHERE user_id = $1`
	err := db.Get(&profile, query, userID)
	if err != nil {
		if err.Error() == "sql: no rows in result set" {
			return nil, fmt.Errorf("profile not found")
		}
		return nil, fmt.Errorf("failed to get profile: %w", err)
	}
	return &profile, nil
}

// CreateProfile creates a new user profile
func CreateProfile(db *sqlx.DB, userID int, req models.UpdateProfileRequest) (*models.UserProfile, error) {
	var profile models.UserProfile
	query := `INSERT INTO user_profiles (user_id, height, measurement_unit, created_at, updated_at) VALUES ($1, $2, $3, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP) RETURNING id, user_id, height, measurement_unit, profile_image_path, created_at, updated_at`
	err := db.QueryRowx(query, userID, req.Height, req.MeasurementUnit).StructScan(&profile)
	if err != nil {
		return nil, fmt.Errorf("failed to create profile: %w", err)
	}
	return &profile, nil
}

// UpdateProfile updates a user's profile information
func UpdateProfile(db *sqlx.DB, userID int, req models.UpdateProfileRequest) (*models.UserProfile, error) {
	query := `UPDATE user_profiles SET height = $1, measurement_unit = $2, updated_at = CURRENT_TIMESTAMP WHERE user_id = $3 RETURNING id, user_id, height, measurement_unit, profile_image_path, created_at, updated_at`
	var profile models.UserProfile
	err := db.QueryRowx(query, req.Height, req.MeasurementUnit, userID).StructScan(&profile)
	if err != nil {
		if err.Error() == "sql: no rows in result set" {
			return nil, fmt.Errorf("profile not found")
		}
		return nil, fmt.Errorf("failed to update profile: %w", err)
	}
	return &profile, nil
}

// SaveProfileImage saves the path to a user's profile image
func SaveProfileImage(db *sqlx.DB, userID int, imagePath string) (*models.UserProfile, error) {
	query := `UPDATE user_profiles SET profile_image_path = $1, updated_at = CURRENT_TIMESTAMP WHERE user_id = $2 RETURNING id, user_id, height, measurement_unit, profile_image_path, created_at, updated_at`
	var profile models.UserProfile
	err := db.QueryRowx(query, &imagePath, userID).StructScan(&profile)
	if err != nil {
		if err.Error() == "sql: no rows in result set" {
			return nil, fmt.Errorf("profile not found")
		}
		return nil, fmt.Errorf("failed to save profile image: %w", err)
	}
	return &profile, nil
}
