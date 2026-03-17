package routes

import (
	"net/http"

	"github.com/gin-gonic/gin"
	"openiron-api/handlers"
	"openiron-api/middleware"
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
	users := router.Group("/users")
	users.Use(middleware.AuthMiddleware())
	{
		users.GET("/me/profile", handlers.GetMyProfile)             // GET /api/v1/users/me/profile
		users.PUT("/me/profile", handlers.UpdateMyProfile)          // PUT /api/v1/users/me/profile
		users.POST("/me/profile/image", handlers.UploadProfileImage) // POST /api/v1/users/me/profile/image
		users.POST("/me/change-password", handlers.ChangePassword)  // POST /api/v1/users/me/change-password
		users.PUT("/:id/username", handlers.UpdateUsername)         // PUT /api/v1/users/:id/username (user or admin)
	}
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
	admin := router.Group("/admin")
	admin.Use(middleware.AuthMiddleware())
	admin.Use(middleware.AdminMiddleware())
	{
		admin.POST("/users", handlers.CreateUser)          // POST /api/v1/admin/users
		admin.GET("/users", handlers.ListUsers)            // GET /api/v1/admin/users
		admin.GET("/users/:id", handlers.GetUser)          // GET /api/v1/admin/users/:id
		admin.DELETE("/users/:id", handlers.DeleteUser)    // DELETE /api/v1/admin/users/:id
		admin.POST("/users/:id/reset-password", handlers.ResetPassword) // POST /api/v1/admin/users/:id/reset-password
		admin.PUT("/users/:id/role", handlers.UpdateUserRole) // PUT /api/v1/admin/users/:id/role
		admin.PUT("/users/:id/username", handlers.UpdateUsername) // PUT /api/v1/admin/users/:id/username
	}
}
