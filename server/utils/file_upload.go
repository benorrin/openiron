package utils

import (
	"fmt"
	"io"
	"os"
	"path/filepath"
	"strings"
)

const (
	MaxUploadSize = 10 * 1024 * 1024 // 10MB
	UploadDir     = "uploads"
)

// SaveFile saves an uploaded file to the filesystem
func SaveFile(file io.Reader, destinationPath string) error {
	// Create directories if they don't exist
	dir := filepath.Dir(destinationPath)
	if err := os.MkdirAll(dir, 0755); err != nil {
		return fmt.Errorf("failed to create directory: %w", err)
	}

	// Create the file
	out, err := os.Create(destinationPath)
	if err != nil {
		return fmt.Errorf("failed to create file: %w", err)
	}
	defer out.Close()

	// Copy the uploaded file to the destination
	if _, err := io.Copy(out, file); err != nil {
		return fmt.Errorf("failed to save file: %w", err)
	}

	return nil
}

// DeleteFile deletes a file from the filesystem
func DeleteFile(filePath string) error {
	return os.Remove(filePath)
}

// GetProfileImagePath returns the path where profile images are stored
func GetProfileImagePath(userID int, filename string) string {
	return filepath.Join(UploadDir, "users", fmt.Sprintf("%d", userID), filename)
}

// GetMetricPhotoPath returns the path where metric photos are stored
func GetMetricPhotoPath(userID, metricID int, filename string) string {
	return filepath.Join(UploadDir, "users", fmt.Sprintf("%d", userID), "body_metrics", fmt.Sprintf("%d", metricID), filename)
}

// ValidateImageFile checks if the file is a valid image
func ValidateImageFile(filename string) bool {
	allowedExtensions := []string{".jpg", ".jpeg", ".png", ".gif"}
	ext := strings.ToLower(filepath.Ext(filename))
	for _, allowed := range allowedExtensions {
		if ext == allowed {
			return true
		}
	}
	return false
}
