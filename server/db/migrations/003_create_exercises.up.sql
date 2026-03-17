CREATE TABLE exercises (
  id SERIAL PRIMARY KEY,
  name VARCHAR(255) NOT NULL,
  type VARCHAR(50) NOT NULL CHECK (type IN ('strength', 'cardio', 'flexibility')),
  target_muscles JSONB DEFAULT '[]'::jsonb, -- Array of muscle groups
  description TEXT,
  created_by VARCHAR(50) NOT NULL, -- "system" or "user:123"
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
