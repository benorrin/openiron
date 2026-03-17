CREATE TABLE body_metrics (
  id SERIAL PRIMARY KEY,
  user_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  date TIMESTAMP NOT NULL,
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE body_metric_values (
  id SERIAL PRIMARY KEY,
  body_metric_id INTEGER NOT NULL REFERENCES body_metrics(id) ON DELETE CASCADE,
  metric_type VARCHAR(100) NOT NULL, -- "weight", "waist", "bicep", etc.
  value FLOAT NOT NULL,
  unit VARCHAR(50) NOT NULL, -- "lbs", "kg", "in", "cm"
  notes TEXT,
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE body_metric_photos (
  id SERIAL PRIMARY KEY,
  body_metric_id INTEGER NOT NULL REFERENCES body_metrics(id) ON DELETE CASCADE,
  photo_path VARCHAR(255) NOT NULL,
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
