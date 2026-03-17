package handlers

import (
	"github.com/gin-gonic/gin"
)

// CreateUser creates a new user account (admin only)
func CreateUser(c *gin.Context) {
	// POST /api/v1/admin/users (admin only)
}

// ListUsers retrieves all users (admin only)
func ListUsers(c *gin.Context) {
	// GET /api/v1/admin/users (admin only)
}

// GetUser retrieves a specific user by ID (admin only)
func GetUser(c *gin.Context) {
	// GET /api/v1/admin/users/:id (admin only)
}

// DeleteUser removes a user account (admin only)
func DeleteUser(c *gin.Context) {
	// DELETE /api/v1/admin/users/:id (admin only)
}

// ResetPassword resets a user's password (admin only)
func ResetPassword(c *gin.Context) {
	// POST /api/v1/admin/users/:id/reset-password (admin only)
}

// GetMyProfile retrieves the current user's profile
func GetMyProfile(c *gin.Context) {
	// GET /api/v1/me (authenticated)
}

func UpdateMyProfile(c *gin.Context) {
	// PUT /api/v1/me (authenticated)
}

func ChangePassword(c *gin.Context) {
	// POST /api/v1/me/password (authenticated)
}

func UploadProfileImage(c *gin.Context) {
	// POST /api/v1/me/profile-image (authenticated)
}
