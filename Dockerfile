# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY LexCore.sln .
COPY src/LexCore.API/LexCore.API.csproj src/LexCore.API/
COPY src/LexCore.Application/LexCore.Application.csproj src/LexCore.Application/
COPY src/LexCore.Domain/LexCore.Domain.csproj src/LexCore.Domain/
COPY src/LexCore.Infrastructure/LexCore.Infrastructure.csproj src/LexCore.Infrastructure/

# Restore dependencies
RUN dotnet restore

# Copy all source code
COPY . .

# Build the application
WORKDIR /src/src/LexCore.API
RUN dotnet build -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install libgdiplus for QuestPDF
RUN apt-get update && apt-get install -y libgdiplus && rm -rf /var/lib/apt/lists/*

# Create uploads directory
RUN mkdir -p /app/uploads

COPY --from=publish /app/publish .

# Set environment
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 5000

ENTRYPOINT ["dotnet", "LexCore.API.dll"]
