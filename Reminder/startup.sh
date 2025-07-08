#!/bin/bash

# Exit on any error
set -e

echo "Starting Reminder application..."

# Wait for database to be ready (if needed)
echo "Checking database connectivity..."
until dotnet ef database update --verbose; do
    echo "Database is unavailable - sleeping"
    sleep 2
done

echo "Database is ready - starting application"

# Start the application
exec dotnet Reminder.dll 