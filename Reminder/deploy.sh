#!/bin/bash

# Exit on any error
set -e

echo "Starting deployment process..."

# Apply database migrations
echo "Applying database migrations..."
dotnet ef database update

echo "Deployment completed successfully!" 