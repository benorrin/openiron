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
func CreateUser(username, email, password, role string) error {
	// Create user with validation
	return nil
}

// GetUser retrieves a user by their ID
func GetUser(userID int) error {
	// Get user by ID
	return nil
}

// GetAllUsers retrieves all users from the database
func GetAllUsers() error {
	// Get all users
	return nil
}

// DeleteUser removes a user from the database
func DeleteUser(userID int) error {
	// Delete user by ID
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

	return user.ID, user.Role, nil
}

// GetUserRole retrieves the role of a user by their ID
func GetUserRole(userID int) (string, error) {
	// Get user role by ID
	return "", nil
}

// ChangePassword updates a user's password
func ChangePassword(userID int, oldPassword, newPassword string) error {
	// Change user password
	return nil
}
