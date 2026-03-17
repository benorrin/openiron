package utils

import (
	"net/http"

	"github.com/gin-gonic/gin"
)

// SuccessResponse returns a success JSON response
func SuccessResponse(c *gin.Context, statusCode int, data interface{}) {
	c.JSON(statusCode, gin.H{
		"data": data,
	})
}

// ErrorResponse returns an error JSON response
func ErrorResponse(c *gin.Context, statusCode int, errorMsg, errorCode string) {
	c.JSON(statusCode, gin.H{
		"error":  errorMsg,
		"code":   errorCode,
		"status": statusCode,
	})
}

// ErrorBadRequest returns a 400 error
func ErrorBadRequest(c *gin.Context, msg string) {
	ErrorResponse(c, http.StatusBadRequest, msg, "BAD_REQUEST")
}

// ErrorUnauthorized returns a 401 error
func ErrorUnauthorized(c *gin.Context, msg string) {
	ErrorResponse(c, http.StatusUnauthorized, msg, "UNAUTHORIZED")
}

// ErrorForbidden returns a 403 error
func ErrorForbidden(c *gin.Context, msg string) {
	ErrorResponse(c, http.StatusForbidden, msg, "FORBIDDEN")
}

// ErrorNotFound returns a 404 error
func ErrorNotFound(c *gin.Context, msg string) {
	ErrorResponse(c, http.StatusNotFound, msg, "NOT_FOUND")
}

// ErrorInternalServer returns a 500 error
func ErrorInternalServer(c *gin.Context, msg string) {
	ErrorResponse(c, http.StatusInternalServerError, msg, "INTERNAL_SERVER_ERROR")
}
