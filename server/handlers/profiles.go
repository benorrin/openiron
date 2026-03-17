package handlers

import (
	"net/http"

	"github.com/gin-gonic/gin"
	"openiron-api/db"
	"openiron-api/models"
	"openiron-api/services"
	"openiron-api/utils"
)

// GetMyProfile retrieves the current user's profile
func GetMyProfile(c *gin.Context) {
	userID := c.MustGet("user_id").(int)

	profile, err := services.GetProfile(db.DB, userID)
	if err != nil {
		if err.Error() == "profile not found" {
			utils.ErrorResponse(c, http.StatusNotFound, "Profile not found", "PROFILE_NOT_FOUND")
		} else {
			utils.ErrorResponse(c, http.StatusInternalServerError, "Failed to retrieve profile", err.Error())
		}
		return
	}

	utils.SuccessResponse(c, http.StatusOK, profile)
}

// UpdateMyProfile updates the current user's profile
func UpdateMyProfile(c *gin.Context) {
	userID := c.MustGet("user_id").(int)

	var req models.UpdateProfileRequest
	if err := c.ShouldBindJSON(&req); err != nil {
		utils.ErrorResponse(c, http.StatusBadRequest, "Invalid request data", err.Error())
		return
	}

	// Try to get existing profile first
	_, err := services.GetProfile(db.DB, userID)
	var profile *models.UserProfile

	if err != nil && err.Error() == "profile not found" {
		// Create new profile if it doesn't exist
		profile, err = services.CreateProfile(db.DB, userID, req)
		if err != nil {
			utils.ErrorResponse(c, http.StatusInternalServerError, "Failed to create profile", err.Error())
			return
		}
	} else if err != nil {
		utils.ErrorResponse(c, http.StatusInternalServerError, "Failed to retrieve profile", err.Error())
		return
	} else {
		// Update existing profile
		profile, err = services.UpdateProfile(db.DB, userID, req)
		if err != nil {
			utils.ErrorResponse(c, http.StatusInternalServerError, "Failed to update profile", err.Error())
			return
		}
	}

	utils.SuccessResponse(c, http.StatusOK, profile)
}

// UploadProfileImage handles profile image uploads
func UploadProfileImage(c *gin.Context) {
	userID := c.MustGet("user_id").(int)

	// Get the file from the request
	file, err := c.FormFile("file")
	if err != nil {
		utils.ErrorResponse(c, http.StatusBadRequest, "No file provided", "NO_FILE")
		return
	}

	// Validate file size (5MB max)
	if file.Size > 5*1024*1024 {
		utils.ErrorResponse(c, http.StatusBadRequest, "File size exceeds 5MB limit", "FILE_TOO_LARGE")
		return
	}

	// Validate image file type
	if !utils.ValidateImageFile(file.Filename) {
		utils.ErrorResponse(c, http.StatusBadRequest, "Invalid file type, only JPG, PNG, GIF allowed", "INVALID_FILE_TYPE")
		return
	}

	// Get profile image path
	filePath := utils.GetProfileImagePath(userID, file.Filename)

	// Save the file
	src, err := file.Open()
	if err != nil {
		utils.ErrorResponse(c, http.StatusInternalServerError, "Failed to read file", err.Error())
		return
	}
	defer src.Close()

	if err := utils.SaveFile(src, filePath); err != nil {
		utils.ErrorResponse(c, http.StatusInternalServerError, "Failed to save file", err.Error())
		return
	}

	// Update profile with image path
	profile, err := services.SaveProfileImage(db.DB, userID, filePath)
	if err != nil {
		if err.Error() == "profile not found" {
			utils.ErrorResponse(c, http.StatusNotFound, "Profile not found", "PROFILE_NOT_FOUND")
		} else {
			utils.ErrorResponse(c, http.StatusInternalServerError, "Failed to update profile image", err.Error())
		}
		return
	}

	utils.SuccessResponse(c, http.StatusOK, profile)
}
