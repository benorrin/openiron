package services

// LogMetric records body measurements for a user
func LogMetric(userID int, date string, measurements []interface{}) error {
	// Log body metrics for a user
	return nil
}

// GetMetrics retrieves a user's body metric history
func GetMetrics(userID int, limit, offset int) error {
	// Get user's metric history
	return nil
}

// GetMetricByID retrieves a specific body metric entry
func GetMetricByID(metricID int) error {
	// Get specific metric entry with photos
	return nil
}

// UpdateMetric modifies an existing body metric entry
func UpdateMetric(metricID int, measurements []interface{}) error {
	// Update metric entry
	return nil
}

// DeleteMetric removes a body metric entry
func DeleteMetric(metricID int) error {
	// Delete metric entry
	return nil
}

// SaveMetricPhoto associates a photo with a body metric entry
func SaveMetricPhoto(metricID int, photoPath string) error {
	// Save photo to metric
	return nil
}

func DeleteMetricPhoto(photoID int) error {
	// Delete photo from metric
	return nil
}
