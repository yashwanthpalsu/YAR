# Use the official .NET 8.0 runtime image as the base image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

# Use the official .NET 8.0 SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the project file and restore dependencies
COPY Reminder/Reminder.csproj ./Reminder/
RUN dotnet restore ./Reminder/Reminder.csproj

# Copy the rest of the source code
COPY Reminder/. ./Reminder/

# Build the application
RUN dotnet build ./Reminder/Reminder.csproj -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish ./Reminder/Reminder.csproj -c Release -o /app/publish

# Build the final runtime image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Create a non-root user
RUN adduser --disabled-password --gecos '' appuser && chown -R appuser /app
USER appuser

ENTRYPOINT ["dotnet", "Reminder.dll"] 