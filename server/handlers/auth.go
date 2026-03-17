package handlers

import (
	"github.com/gin-gonic/gin"
)

// Login authenticates a user and returns a JWT token
func Login(c *gin.Context) {
	// POST /api/v1/auth/login
}

// RefreshToken generates a new JWT token for an authenticated user
func RefreshToken(c *gin.Context) {
	// POST /api/v1/auth/refresh
}
