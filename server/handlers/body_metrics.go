package handlers

import (
	"github.com/gin-gonic/gin"
)

// LogMetric records new body measurements
func LogMetric(c *gin.Context) {
	// POST /api/v1/body-metrics
}

// ListMetrics retrieves user's body metric history
func ListMetrics(c *gin.Context) {
	// GET /api/v1/body-metrics
}

// GetMetric retrieves a specific body metric entry
func GetMetric(c *gin.Context) {
	// GET /api/v1/body-metrics/:id
}

// UpdateMetric modifies an existing body metric entry
func UpdateMetric(c *gin.Context) {
	// PUT /api/v1/body-metrics/:id
}

// DeleteMetric removes a body metric entry
func DeleteMetric(c *gin.Context) {
	// DELETE /api/v1/body-metrics/:id
}

// UploadMetricPhoto uploads a progress photo for a body metric entry
func UploadMetricPhoto(c *gin.Context) {
	// POST /api/v1/body-metrics/:id/photos
}

func DeleteMetricPhoto(c *gin.Context) {
	// DELETE /api/v1/body-metrics/:id/photos/:photoId
}
