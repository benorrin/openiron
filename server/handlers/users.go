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

// ChangePassword allows a user to change their own password
func ChangePassword(c *gin.Context) {
	userID := c.MustGet("user_id").(int)

	var req models.ChangePasswordRequest
	if err := c.ShouldBindJSON(&req); err != nil {
		utils.ErrorResponse(c, http.StatusBadRequest, "Invalid request data", err.Error())
		return
	}

	err := services.ChangePassword(db.DB, userID, req.OldPassword, req.NewPassword)
	if err != nil {
		if err.Error() == "invalid old password" {
			utils.ErrorResponse(c, http.StatusUnauthorized, "Invalid old password", "INVALID_OLD_PASSWORD")
		} else {
			utils.ErrorResponse(c, http.StatusInternalServerError, "Failed to change password", err.Error())
		}
		return
	}

	c.JSON(http.StatusOK, gin.H{"message": "password changed successfully"})
}

// UpdateUserRole updates a user's role (admin only)
func UpdateUserRole(c *gin.Context) {
	userID, err := strconv.Atoi(c.Param("id"))
	if err != nil {
		utils.ErrorResponse(c, http.StatusBadRequest, "Invalid user ID", err.Error())
		return
	}

	var req models.UpdateUserRoleRequest
	if err := c.ShouldBindJSON(&req); err != nil {
		utils.ErrorResponse(c, http.StatusBadRequest, "Invalid request data", err.Error())
		return
	}

	user, err := services.UpdateUserRole(db.DB, userID, req.Role)
	if err != nil {
		if err.Error() == "user not found" {
			utils.ErrorResponse(c, http.StatusNotFound, "User not found", "USER_NOT_FOUND")
		} else {
			utils.ErrorResponse(c, http.StatusInternalServerError, "Failed to update user role", err.Error())
		}
		return
	}

	utils.SuccessResponse(c, http.StatusOK, user)
}

// UpdateUsername updates a user's username (admin can change any, user changes own)
func UpdateUsername(c *gin.Context) {
	userID, err := strconv.Atoi(c.Param("id"))
	if err != nil {
		utils.ErrorResponse(c, http.StatusBadRequest, "Invalid user ID", err.Error())
		return
	}

	// Check if user is trying to change their own username or if they're admin
	currentUserID := c.MustGet("user_id").(int)
	currentUserRole := c.MustGet("role").(string)
	if currentUserID != userID && currentUserRole != "admin" {
		utils.ErrorResponse(c, http.StatusForbidden, "Cannot change another user's username", "FORBIDDEN")
		return
	}

	var req models.UpdateUsernameRequest
	if err := c.ShouldBindJSON(&req); err != nil {
		utils.ErrorResponse(c, http.StatusBadRequest, "Invalid request data", err.Error())
		return
	}

	user, err := services.UpdateUsername(db.DB, userID, req.Username)
	if err != nil {
		if err.Error() == "user not found" {
			utils.ErrorResponse(c, http.StatusNotFound, "User not found", "USER_NOT_FOUND")
		} else if err.Error() == "username already exists" {
			utils.ErrorResponse(c, http.StatusConflict, "Username already taken", "USERNAME_TAKEN")
		} else {
			utils.ErrorResponse(c, http.StatusInternalServerError, "Failed to update username", err.Error())
		}
		return
	}

	utils.SuccessResponse(c, http.StatusOK, user)
}