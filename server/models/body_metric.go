package models

import (
	"database/sql/driver"
	"encoding/json"
	"time"
)

type BodyMetric struct {
	ID        int       `db:"id" json:"id"`
	UserID    int       `db:"user_id" json:"user_id"`
	Date      time.Time `db:"date" json:"date"`
	CreatedAt time.Time `db:"created_at" json:"created_at"`
	UpdatedAt time.Time `db:"updated_at" json:"updated_at"`
	Values    []BodyMetricValue `json:"values,omitempty"` // Loaded separately
	Photos    []BodyMetricPhoto `json:"photos,omitempty"` // Loaded separately
}

type BodyMetricValue struct {
	ID            int       `db:"id" json:"id"`
	BodyMetricID  int       `db:"body_metric_id" json:"body_metric_id"`
	MetricType    string    `db:"metric_type" json:"metric_type"` // "weight", "waist", "bicep", etc.
	Value         float64   `db:"value" json:"value"`
	Unit          string    `db:"unit" json:"unit"` // "lbs", "kg", "in", "cm"
	Notes         *string   `db:"notes" json:"notes"`
	CreatedAt     time.Time `db:"created_at" json:"created_at"`
}

type BodyMetricPhoto struct {
	ID           int       `db:"id" json:"id"`
	BodyMetricID int       `db:"body_metric_id" json:"body_metric_id"`
	PhotoPath    string    `db:"photo_path" json:"photo_path"`
	CreatedAt    time.Time `db:"created_at" json:"created_at"`
}

// MetricsPayload for bulk metrics logging
type MetricsPayload struct {
	Date         time.Time       `json:"date"`
	Measurements []MeasurementIn `json:"measurements"`
	Notes        *string         `json:"notes"`
}

type MeasurementIn struct {
	MetricType string  `json:"metric_type"`
	Value      float64 `json:"value"`
	Unit       string  `json:"unit"`
	Notes      *string `json:"notes"`
}

// Helper for scanning/valuing metric data if needed in future
type MetricDataMap map[string]interface{}

func (m *MetricDataMap) Scan(value interface{}) error {
	if value == nil {
		return nil
	}
	bytes, ok := value.([]byte)
	if !ok {
		return nil
	}
	return json.Unmarshal(bytes, &m)
}

func (m MetricDataMap) Value() (driver.Value, error) {
	return json.Marshal(m)
}
