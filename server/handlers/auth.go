package handlers

import (
	"net/http"

	"github.com/gin-gonic/gin"
	"openiron-api/db"
	"openiron-api/middleware"
	"openiron-api/models"
	"openiron-api/services"
	"openiron-api/utils"
)

// Login authenticates a user and returns a JWT token
func Login(c *gin.Context) {
	var req models.LoginRequest
	if err := c.ShouldBindJSON(&req); err != nil {
		utils.ErrorResponse(c, http.StatusBadRequest, "Invalid request data", err.Error())
		return
	}

	// Verify credentials
	userID, role, err := services.VerifyCredentials(db.DB, req.Username, req.Password)
	if err != nil {
		utils.ErrorResponse(c, http.StatusUnauthorized, "Invalid credentials", err.Error())
		return
	}

	// Generate JWT token
	token, expiresAt, err := middleware.GenerateToken(userID, role)
	if err != nil {
		utils.ErrorResponse(c, http.StatusInternalServerError, "Failed to generate token", err.Error())
		return
	}

	response := models.LoginResponse{
		Token:     token,
		ExpiresAt: expiresAt,
	}

	utils.SuccessResponse(c, http.StatusOK, response)
}

// RefreshToken generates a new JWT token for an authenticated user
func RefreshToken(c *gin.Context) {
	// Get user ID from JWT middleware
	userID := c.MustGet("user_id").(int)
	role := c.MustGet("role").(string)

	// Generate new token
	token, expiresAt, err := middleware.GenerateToken(userID, role)
	if err != nil {
		utils.ErrorResponse(c, http.StatusInternalServerError, "Failed to generate token", err.Error())
		return
	}

	response := models.LoginResponse{
		Token:     token,
		ExpiresAt: expiresAt,
	}

	utils.SuccessResponse(c, http.StatusOK, response)
}
