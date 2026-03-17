package services

import (
	"fmt"
	"os"

	"github.com/jmoiron/sqlx"
	"openiron-api/models"
	"openiron-api/utils"
)

// CreateAdminIfNotExists creates a default admin user if no admin exists
func CreateAdminIfNotExists(db *sqlx.DB) error {
	// Check if any admin user exists
	var count int
	err := db.Get(&count, "SELECT COUNT(*) FROM users WHERE role = 'admin'")
	if err != nil {
		return fmt.Errorf("failed to check for admin users: %w", err)
	}

	// If admin exists, do nothing
	if count > 0 {
		return nil
	}

	// Get admin credentials from environment
	adminUsername := os.Getenv("ADMIN_USERNAME")
	adminPassword := os.Getenv("ADMIN_PASSWORD")

	if adminUsername == "" {
		adminUsername = "admin"
	}
	if adminPassword == "" {
		adminPassword = "admin_password_change_this"
	}

	// Hash the password
	hashedPassword, err := utils.HashPassword(adminPassword)
	if err != nil {
		return fmt.Errorf("failed to hash admin password: %w", err)
	}

	// Create the admin user
	query := `INSERT INTO users (username, password_hash, role, created_at) VALUES ($1, $2, 'admin', CURRENT_TIMESTAMP)`
	_, err = db.Exec(query, adminUsername, hashedPassword)
	if err != nil {
		return fmt.Errorf("failed to create admin user: %w", err)
	}

	return nil
}

// CreateUser creates a new user account
func CreateUser(db *sqlx.DB, req models.CreateUserRequest) (*models.User, error) {
	// Hash the password
	hashedPassword, err := utils.HashPassword(req.Password)
	if err != nil {
		return nil, fmt.Errorf("failed to hash password: %w", err)
	}

	// Create the user
	query := `INSERT INTO users (username, password_hash, role, created_at) VALUES ($1, $2, $3, CURRENT_TIMESTAMP) RETURNING id, username, role, created_at`
	var user models.User
	err = db.QueryRowx(query, req.Username, hashedPassword, req.Role).StructScan(&user)
	if err != nil {
		return nil, fmt.Errorf("failed to create user: %w", err)
	}

	return &user, nil
}

// GetUser retrieves a user by their ID
func GetUser(db *sqlx.DB, userID int) (*models.User, error) {
	var user models.User
	query := `SELECT id, username, role, created_at FROM users WHERE id = $1`
	err := db.Get(&user, query, userID)
	if err != nil {
		if err.Error() == "sql: no rows in result set" {
			return nil, fmt.Errorf("user not found")
		}
		return nil, fmt.Errorf("failed to get user: %w", err)
	}
	return &user, nil
}

// GetAllUsers retrieves all users from the database
func GetAllUsers(db *sqlx.DB) ([]models.User, error) {
	var users []models.User
	query := `SELECT id, username, role, created_at FROM users ORDER BY username`
	err := db.Select(&users, query)
	if err != nil {
		return nil, fmt.Errorf("failed to get users: %w", err)
	}
	return users, nil
}

// DeleteUser removes a user from the database
func DeleteUser(db *sqlx.DB, userID int) error {
	query := `DELETE FROM users WHERE id = $1`
	result, err := db.Exec(query, userID)
	if err != nil {
		return fmt.Errorf("failed to delete user: %w", err)
	}

	rowsAffected, err := result.RowsAffected()
	if err != nil {
		return fmt.Errorf("failed to get rows affected: %w", err)
	}

	if rowsAffected == 0 {
		return fmt.Errorf("user not found")
	}

	return nil
}

// VerifyCredentials checks username/password and returns user ID and role
func VerifyCredentials(db *sqlx.DB, username, password string) (int, string, error) {
	var user models.User
	query := `SELECT id, username, password_hash, role FROM users WHERE username = $1`
	err := db.Get(&user, query, username)
	if err != nil {
		if err.Error() == "sql: no rows in result set" {
			return 0, "", fmt.Errorf("invalid credentials")
		}
		return 0, "", fmt.Errorf("database error: %w", err)
	}

	// Verify password
	if !utils.VerifyPassword(user.PasswordHash, password) {
		return 0, "", fmt.Errorf("invalid credentials")
	}

	return user.ID, string(user.Role), nil
}

// GetUserRole retrieves the role of a user by their ID
func GetUserRole(userID int) (string, error) {
	// Get user role by ID
	return "", nil
}

// ChangePassword updates a user's password
func ChangePassword(db *sqlx.DB, userID int, oldPassword, newPassword string) error {
	// First verify the old password
	var user models.User
	query := `SELECT password_hash FROM users WHERE id = $1`
	err := db.Get(&user, query, userID)
	if err != nil {
		if err.Error() == "sql: no rows in result set" {
			return fmt.Errorf("user not found")
		}
		return fmt.Errorf("failed to get user: %w", err)
	}

	// For admin password reset, we skip old password verification
	// (admin can reset any user's password without knowing old password)
	// If oldPassword is provided, verify it matches
	if oldPassword != "" {
		if !utils.VerifyPassword(user.PasswordHash, oldPassword) {
			return fmt.Errorf("invalid old password")
		}
	}

	// Hash the new password
	hashedPassword, err := utils.HashPassword(newPassword)
	if err != nil {
		return fmt.Errorf("failed to hash new password: %w", err)
	}

	// Update the password
	updateQuery := `UPDATE users SET password_hash = $1 WHERE id = $2`
	_, err = db.Exec(updateQuery, hashedPassword, userID)
	if err != nil {
		return fmt.Errorf("failed to update password: %w", err)
	}

	return nil
}
