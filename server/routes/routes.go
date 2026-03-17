package routes

import (
	"net/http"

	"github.com/gin-gonic/gin"
)

func SetupRoutes(router *gin.Engine) {
	// Health check endpoint
	router.GET("/health", func(c *gin.Context) {
		c.JSON(http.StatusOK, gin.H{
			"status": "ok",
		})
	})

	// API v1 base group
	api := router.Group("/api/v1")
	{
		// Auth routes (no auth required)
		setupAuthRoutes(api)

		// User routes (authenticated)
		setupUserRoutes(api)

		// Exercise routes
		setupExerciseRoutes(api)

		// Body metrics routes (authenticated)
		setupMetricsRoutes(api)

		// Workout routes (authenticated)
		setupWorkoutRoutes(api)

		// Admin routes (admin only)
		setupAdminRoutes(api)
	}

	// 404 handler
	router.NoRoute(func(c *gin.Context) {
		c.JSON(http.StatusNotFound, gin.H{
			"error": "endpoint not found",
			"code":  "NOT_FOUND",
		})
	})
}

func setupAuthRoutes(router *gin.RouterGroup) {
	auth := router.Group("/auth")
	{
		auth.POST("/login", handlers.Login) // POST /api/v1/auth/login
	}

	// Protected auth routes
	authProtected := router.Group("/auth")
	authProtected.Use(middleware.AuthMiddleware())
	{
		authProtected.POST("/refresh", handlers.RefreshToken) // POST /api/v1/auth/refresh
	}
}

func setupUserRoutes(router *gin.RouterGroup) {
	// TODO: User profile management
	// Profile updates, password changes, avatar uploads
}

func setupExerciseRoutes(router *gin.RouterGroup) {
	// TODO: Exercise library endpoints
	// CRUD operations for exercises, muscle group filtering
}

func setupMetricsRoutes(router *gin.RouterGroup) {
	// TODO: Body metrics tracking
	// Weight, measurements, progress photos
}

func setupWorkoutRoutes(router *gin.RouterGroup) {
	// TODO: Workout logging and management
	// Create workouts, add exercises, track progress
}

func setupAdminRoutes(router *gin.RouterGroup) {
	// TODO: Admin user management
	// Create users, manage accounts, reset passwords
}
