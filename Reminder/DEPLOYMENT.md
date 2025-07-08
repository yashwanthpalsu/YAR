# Deployment Guide for Render

This guide will help you deploy your Reminder application to Render.

## Prerequisites

1. A Render account
2. Your application code pushed to a Git repository (GitHub, GitLab, etc.)
3. Email service credentials (Gmail, SendGrid, etc.)
4. Twilio account credentials (for SMS functionality)

## Step 1: Prepare Your Repository

Ensure your repository contains all the necessary files:
- `render.yaml` - Render configuration
- `Dockerfile` - Container configuration
- `appsettings.Production.json` - Production settings
- `.dockerignore` - Docker build exclusions

## Step 2: Set Up Render Services

### Option A: Using render.yaml (Recommended)

1. Connect your Git repository to Render
2. Render will automatically detect the `render.yaml` file
3. The configuration will create both the web service and PostgreSQL database

### Option B: Manual Setup

#### 2.1 Create PostgreSQL Database

1. Go to your Render dashboard
2. Click "New" → "PostgreSQL"
3. Configure:
   - **Name**: `reminder-db`
   - **Database**: `reminder`
   - **User**: `reminder_user`
   - **Plan**: Starter (or your preferred plan)

#### 2.2 Create Web Service

1. Click "New" → "Web Service"
2. Connect your Git repository
3. Configure:
   - **Name**: `reminder-app`
   - **Environment**: `dotnet`
   - **Build Command**: `dotnet build --configuration Release`
   - **Start Command**: `dotnet run --configuration Release`
   - **Plan**: Starter (or your preferred plan)

## Step 3: Configure Environment Variables

In your Render web service dashboard, add the following environment variables:

### Database Variables (Auto-configured if using render.yaml)
- `POSTGRES_HOST` - From database service
- `POSTGRES_PORT` - From database service  
- `POSTGRES_DB` - From database service
- `POSTGRES_USER` - From database service
- `POSTGRES_PASSWORD` - From database service

### Application Variables
- `ASPNETCORE_ENVIRONMENT`: `Production`
- `ASPNETCORE_URLS`: `http://0.0.0.0:$PORT`
- `APP_URL`: Your application URL (e.g., `https://your-app-name.onrender.com`)

### Email Configuration
- `SMTP_SERVER`: Your SMTP server (e.g., `smtp.gmail.com`)
- `SMTP_PORT`: `587`
- `SMTP_USERNAME`: Your email username
- `SMTP_PASSWORD`: Your email password/app password
- `SMTP_FROM`: Your sender email address

### Twilio Configuration
- `TWILIO_ACCOUNT_SID`: Your Twilio Account SID
- `TWILIO_AUTH_TOKEN`: Your Twilio Auth Token
- `TWILIO_FROM_PHONE`: Your Twilio phone number

## Step 4: Deploy

1. Push your changes to your Git repository
2. Render will automatically build and deploy your application
3. Monitor the build logs for any issues

## Step 5: Verify Deployment

1. Check that your application is accessible at the provided URL
2. Verify database connectivity
3. Test email and SMS functionality
4. Check Hangfire dashboard at `/hangfire`

## Troubleshooting

### Common Issues

1. **Database Connection Errors**
   - Verify environment variables are correctly set
   - Check database service is running
   - Ensure database migrations are applied

2. **Build Failures**
   - Check build logs for specific errors
   - Verify all dependencies are included in `Reminder.csproj`
   - Ensure Dockerfile is properly configured

3. **Runtime Errors**
   - Check application logs in Render dashboard
   - Verify all environment variables are set
   - Test locally with production configuration

### Logs

- Application logs are available in the Render dashboard
- Database logs can be viewed in the PostgreSQL service dashboard
- Build logs show compilation and deployment progress

## Security Considerations

1. **Environment Variables**: Never commit sensitive data to your repository
2. **Database**: Use strong passwords and restrict access
3. **HTTPS**: Render provides automatic HTTPS certificates
4. **Secrets**: Use Render's secret management for sensitive data

## Scaling

- Upgrade your plan to handle more traffic
- Consider using Render's auto-scaling features
- Monitor performance metrics in the dashboard

## Support

- Render Documentation: https://render.com/docs
- .NET Documentation: https://docs.microsoft.com/en-us/dotnet/
- Entity Framework: https://docs.microsoft.com/en-us/ef/ 