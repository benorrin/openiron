package handlers

import (
	"net/http"
	"strconv"

	"github.com/gin-gonic/gin"
	"github.com/jmoiron/sqlx"
	"openiron-api/db"
	"openiron-api/models"
	"openiron-api/services"
	"openiron-api/utils"
)

// CreateUser creates a new user account (admin only)
func CreateUser(c *gin.Context) {
	var req models.CreateUserRequest
	if err := c.ShouldBindJSON(&req); err != nil {
		utils.ErrorResponse(c, http.StatusBadRequest, "Invalid request data", err.Error())
		return
	}

	user, err := services.CreateUser(db.DB, req)
	if err != nil {
		utils.ErrorResponse(c, http.StatusInternalServerError, "Failed to create user", err.Error())
		return
	}

	utils.SuccessResponse(c, http.StatusCreated, user)
}

// ListUsers retrieves all users (admin only)
func ListUsers(c *gin.Context) {
	users, err := services.GetAllUsers(db.DB)
	if err != nil {
		utils.ErrorResponse(c, http.StatusInternalServerError, "Failed to retrieve users", err.Error())
		return
	}

	utils.SuccessResponse(c, http.StatusOK, users)
}

// GetUser retrieves a specific user by ID (admin only)
func GetUser(c *gin.Context) {
	userID, err := strconv.Atoi(c.Param("id"))
	if err != nil {
		utils.ErrorResponse(c, http.StatusBadRequest, "Invalid user ID", err.Error())
		return
	}

	user, err := services.GetUser(db.DB, userID)
	if err != nil {
		if err.Error() == "user not found" {
			utils.ErrorResponse(c, http.StatusNotFound, "User not found", err.Error())
		} else {
			utils.ErrorResponse(c, http.StatusInternalServerError, "Failed to retrieve user", err.Error())
		}
		return
	}

	utils.SuccessResponse(c, http.StatusOK, user)
}

// DeleteUser removes a user account (admin only)
func DeleteUser(c *gin.Context) {
	userID, err := strconv.Atoi(c.Param("id"))
	if err != nil {
		utils.ErrorResponse(c, http.StatusBadRequest, "Invalid user ID", err.Error())
		return
	}

	err = services.DeleteUser(db.DB, userID)
	if err != nil {
		if err.Error() == "user not found" {
			utils.ErrorResponse(c, http.StatusNotFound, "User not found", err.Error())
		} else {
			utils.ErrorResponse(c, http.StatusInternalServerError, "Failed to delete user", err.Error())
		}
		return
	}

	c.JSON(http.StatusNoContent, gin.H{})
}

// ResetPassword resets a user's password (admin only)
func ResetPassword(c *gin.Context) {
	db := c.MustGet("db").(*sqlx.DB)

	userID, err := strconv.Atoi(c.Param("id"))
	if err != nil {
		utils.ErrorResponse(c, http.StatusBadRequest, "Invalid user ID", err.Error())
		return
	}

	var req models.ResetPasswordRequest
	if err := c.ShouldBindJSON(&req); err != nil {
		utils.ErrorResponse(c, http.StatusBadRequest, "Invalid request data", err.Error())
		return
	}

	err = services.ChangePassword(db, userID, "", req.NewPassword)
	if err != nil {
		utils.ErrorResponse(c, http.StatusInternalServerError, "Failed to reset password", err.Error())
		return
	}

	c.JSON(http.StatusNoContent, gin.H{})
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
