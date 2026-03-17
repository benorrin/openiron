package models

// Request/Response DTOs

// CreateUserRequest for admin creating users
type CreateUserRequest struct {
	Username string `json:"username" binding:"required,min=3"`
	Password string `json:"password" binding:"required,min=6"`
	Role     string `json:"role" binding:"required,oneof=admin user"`
}

// UpdateProfileRequest for users updating their profile
type UpdateProfileRequest struct {
	Height          float64 `json:"height" binding:"required,min=1"`
	MeasurementUnit string  `json:"measurement_unit" binding:"required,oneof=metric imperial"`
}

// ChangePasswordRequest for users changing their password
type ChangePasswordRequest struct {
	OldPassword string `json:"old_password" binding:"required"`
	NewPassword string `json:"new_password" binding:"required,min=6"`
}

// ResetPasswordRequest for admin resetting user passwords
type ResetPasswordRequest struct {
	NewPassword string `json:"new_password" binding:"required,min=6"`
}

// ErrorResponse standard error format
type ErrorResponse struct {
	Error  string `json:"error"`
	Code   string `json:"code"`
	Status int    `json:"status"`
}

// SuccessResponse generic success wrapper
type SuccessResponse struct {
	Data interface{} `json:"data"`
}
