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

### Automatic Configuration (if using render.yaml)

The `render.yaml` file automatically configures most environment variables. However, you still need to set sensitive information manually in the Render dashboard.

### Manual Configuration in Render Dashboard

In your Render web service dashboard, add the following environment variables:

#### Database Variables (Auto-configured if using render.yaml)
- `POSTGRES_HOST` - From database service
- `POSTGRES_PORT` - From database service  
- `POSTGRES_DB` - From database service
- `POSTGRES_USER` - From database service
- `POSTGRES_PASSWORD` - From database service

#### Application Variables
- `ASPNETCORE_ENVIRONMENT`: `Production`
- `ASPNETCORE_URLS`: `http://0.0.0.0:$PORT`
- `APP_URL`: Your application URL (e.g., `https://your-app-name.onrender.com`)

#### Email Configuration (Sensitive - Set in Dashboard)
- `SMTP_SERVER`: `smtp.gmail.com`
- `SMTP_PORT`: `587`
- `SMTP_USERNAME`: Your Gmail address (e.g., `your-app@gmail.com`)
- `SMTP_PASSWORD`: Your Gmail app password (NOT your regular password)
- `SMTP_FROM`: Your Gmail address (e.g., `your-app@gmail.com`)

#### Twilio Configuration (Sensitive - Set in Dashboard)
- `TWILIO_ACCOUNT_SID`: Your Twilio Account SID
- `TWILIO_AUTH_TOKEN`: Your Twilio Auth Token
- `TWILIO_FROM_PHONE`: Your Twilio phone number (e.g., `+1234567890`)

### How to Set Environment Variables in Render Dashboard

1. Go to your Render dashboard
2. Click on your web service
3. Go to the "Environment" tab
4. Click "Add Environment Variable"
5. Add each variable with its corresponding value

### Gmail App Password Setup

For Gmail SMTP, you need to create an App Password:

1. Go to your Google Account settings
2. Navigate to Security → 2-Step Verification
3. Scroll down to "App passwords"
4. Generate a new app password for "Mail"
5. Use this password as your `SMTP_PASSWORD`

### Twilio Setup

1. Create a Twilio account at https://www.twilio.com
2. Get your Account SID and Auth Token from the Twilio Console
3. Purchase a phone number or use a trial number
4. Add these credentials to your environment variables

## Step 4: Deploy

1. Push your changes to your Git repository
2. Render will automatically build and deploy your application
3. Monitor the build logs for any issues

## Step 5: Verify Deployment

1. Check that your application is accessible at the provided URL
2. Verify database connectivity
3. Test email and SMS functionality
4. Check Hangfire dashboard at `/hangfire`

## Security Best Practices

### Environment Variables
- ✅ **DO**: Store sensitive data in environment variables
- ✅ **DO**: Use different credentials for development and production
- ❌ **DON'T**: Commit sensitive data to your repository
- ❌ **DON'T**: Use the same passwords across environments

### Database Security
- ✅ **DO**: Use strong, unique passwords
- ✅ **DO**: Restrict database access to your application only
- ❌ **DON'T**: Use default passwords

### Email Security
- ✅ **DO**: Use Gmail App Passwords (not regular passwords)
- ✅ **DO**: Enable 2FA on your Gmail account
- ❌ **DON'T**: Use your regular Gmail password

## Troubleshooting

### Common Issues

1. **Database Connection Errors**
   - Verify environment variables are correctly set
   - Check database service is running
   - Ensure database migrations are applied

2. **Email Configuration Errors**
   - Verify SMTP credentials are correct
   - Check that you're using Gmail App Password
   - Ensure 2FA is enabled on your Gmail account

3. **Build Failures**
   - Check build logs for specific errors
   - Verify all dependencies are included in `Reminder.csproj`
   - Ensure Dockerfile is properly configured

4. **Runtime Errors**
   - Check application logs in Render dashboard
   - Verify all environment variables are set
   - Test locally with production configuration

### Logs

- Application logs are available in the Render dashboard
- Database logs can be viewed in the PostgreSQL service dashboard
- Build logs show compilation and deployment progress

## Environment Variable Reference

| Variable | Description | Example Value |
|----------|-------------|---------------|
| `ASPNETCORE_ENVIRONMENT` | Application environment | `Production` |
| `ASPNETCORE_URLS` | Application URLs | `http://0.0.0.0:$PORT` |
| `APP_URL` | Your application URL | `https://your-app.onrender.com` |
| `SMTP_SERVER` | SMTP server address | `smtp.gmail.com` |
| `SMTP_PORT` | SMTP port number | `587` |
| `SMTP_USERNAME` | Email username | `your-app@gmail.com` |
| `SMTP_PASSWORD` | Email app password | `abcd efgh ijkl mnop` |
| `SMTP_FROM` | Sender email address | `your-app@gmail.com` |
| `TWILIO_ACCOUNT_SID` | Twilio Account SID | `AC1234567890abcdef` |
| `TWILIO_AUTH_TOKEN` | Twilio Auth Token | `1234567890abcdef` |
| `TWILIO_FROM_PHONE` | Twilio phone number | `+1234567890` | 