# Reminder Application

A comprehensive ASP.NET Core MVC web application for managing reminders with email and SMS notifications.

## Features

- **User Authentication & Authorization**
  - Email and phone verification
  - Password reset functionality
  - User profile management
  - Secure login/logout

- **Reminder Management**
  - Create, edit, and delete reminders
  - Schedule reminders with multiple time slots
  - Set reminder priorities and categories
  - View reminder history

- **Notification System**
  - Email notifications (Gmail SMTP)
  - SMS notifications (Twilio integration)
  - Automated reminder delivery

- **Modern UI/UX**
  - Responsive Bootstrap design
  - FontAwesome icons
  - Professional styling
  - Mobile-friendly interface

## Technology Stack

- **Backend**: ASP.NET Core 8.0 MVC
- **Database**: SQL Server with Entity Framework Core
- **Authentication**: ASP.NET Core Identity
- **Email Service**: Gmail SMTP with MailKit
- **SMS Service**: Twilio API
- **Logging**: Serilog with structured logging
- **UI Framework**: Bootstrap 5 + FontAwesome

## Prerequisites

- .NET 8.0 SDK
- SQL Server (Local or Azure)
- Gmail account (for email notifications)
- Twilio account (for SMS notifications)

## Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/yashwanthpalsu/Reminder.git
   cd Reminder
   ```

2. **Configure the database**
   - Update the connection string in `appsettings.Development.json`
   - Run Entity Framework migrations:
   ```bash
   dotnet ef database update
   ```

3. **Configure email settings**
   - Update Gmail SMTP settings in `appsettings.Development.json`
   - Use an App Password for Gmail authentication

4. **Configure SMS settings**
   - Update Twilio credentials in `appsettings.Development.json`
   - Verify your phone number in Twilio console

5. **Run the application**
   ```bash
   dotnet run
   ```

## Configuration

### Email Configuration
```json
{
  "Email": {
    "SmtpServer": "smtp.gmail.com",
    "Port": 587,
    "Username": "your-email@gmail.com",
    "Password": "your-app-password",
    "From": "your-email@gmail.com",
    "EnableSsl": true
  }
}
```

### SMS Configuration
```json
{
  "Twilio": {
    "AccountSid": "your-account-sid",
    "AuthToken": "your-auth-token",
    "FromPhoneNumber": "+1234567890"
  }
}
```

## Project Structure

```
Reminder/
├── Controllers/          # MVC Controllers
│   ├── AccountController.cs
│   └── HomeController.cs
├── Models/              # Data Models and ViewModels
│   ├── DBEntities/      # Entity Framework Models
│   └── Auth/           # Authentication DTOs
├── Services/           # Business Logic Services
│   ├── AuthService.cs
│   ├── EmailService.cs
│   ├── SmsService.cs
│   └── ReminderService.cs
├── Views/              # Razor Views
│   ├── Account/        # Authentication Views
│   ├── Home/          # Home Page Views
│   └── Shared/        # Layout and Shared Views
├── Migrations/         # Entity Framework Migrations
└── wwwroot/           # Static Files (CSS, JS, Images)
```

## API Endpoints

### Authentication
- `POST /Account/Register` - User registration
- `POST /Account/Login` - User login
- `GET /Account/VerifyEmail` - Email verification
- `POST /Account/ForgotPassword` - Password reset request
- `POST /Account/ResetPassword` - Password reset

### Reminders
- `GET /Home` - Dashboard with reminders
- `POST /API/ReminderCRUD/Create` - Create new reminder
- `PUT /API/ReminderCRUD/Update` - Update reminder
- `DELETE /API/ReminderCRUD/Delete` - Delete reminder

## Security Features

- Password hashing with ASP.NET Core Identity
- Email and phone verification
- CSRF protection with anti-forgery tokens
- Secure session management
- Input validation and sanitization

## Deployment

### Render Deployment (Recommended)

This application is configured for easy deployment on Render. See [DEPLOYMENT.md](DEPLOYMENT.md) for detailed instructions.

**Quick Start:**
1. Push your code to a Git repository
2. Connect your repository to Render
3. Render will automatically detect the `render.yaml` configuration
4. Set up your environment variables in the Render dashboard
5. Deploy!

### Other Platforms

This application can also be deployed to:
- Azure App Service
- Railway.app
- Any platform supporting .NET Core

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Submit a pull request

## License

This project is licensed under the MIT License.

## Support

For support and questions, please open an issue on GitHub. 

# Rasa NLU Integration for Intent Extraction

## Setup Instructions

1. **Install Rasa:**
   ```bash
   pip install rasa
   ```

2. **Initialize Rasa Project:**
   ```bash
   rasa init --no-prompt
   ```
   This creates a `rasa` directory with example files.

3. **Replace `rasa/data/nlu.yml` with the following sample:**

```yaml
version: "3.1"
nlu:
- intent: create_reminder
  examples: |
    - Remind me to [call mom](task) at [5pm](time)
    - Set a reminder for [meeting](task) at [3pm](time)
    - Remind me about [doctor appointment](task) tomorrow
    - I need to [buy groceries](task) at [6pm](time)
- intent: delete_reminder
  examples: |
    - Delete my [doctor appointment](task) reminder
    - Remove the [meeting](task) reminder
    - Cancel the [call mom](task) reminder
- intent: list_reminders
  examples: |
    - What are my reminders?
    - List all reminders
    - Show my reminders
- intent: update_reminder
  examples: |
    - Change my [meeting](task) reminder to [4pm](time)
    - Update the [call mom](task) reminder
```

4. **Train the Model:**
   ```bash
   rasa train
   ```

5. **Run Rasa NLU Server:**
   ```bash
   rasa run --enable-api
   ```
   The API will be available at `http://localhost:5005/model/parse`.

6. **Test the API:**
   ```bash
   curl -X POST "http://localhost:5005/model/parse" -H "Content-Type: application/json" -d '{"text": "Remind me to call mom at 5pm"}'
   ```

## .NET Integration

- The backend will POST user messages to the Rasa API and use the returned intent/entities for API logic.
- See `Services/NluService.cs` for implementation details. 