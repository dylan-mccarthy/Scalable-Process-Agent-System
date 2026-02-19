# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["BusinessProcessAgents.sln", "./"]
COPY ["src/ControlPlane.Api/ControlPlane.Api.csproj", "src/ControlPlane.Api/"]

# Restore dependencies
RUN dotnet restore "src/ControlPlane.Api/ControlPlane.Api.csproj"

# Copy source code
COPY ["src/ControlPlane.Api/", "src/ControlPlane.Api/"]

# Build the application
WORKDIR "/src/src/ControlPlane.Api"
RUN dotnet build "ControlPlane.Api.csproj" -c Release -o /app/build --no-restore

# Publish stage
FROM build AS publish
RUN dotnet publish "ControlPlane.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Create non-root user
RUN groupadd -r appuser && useradd -r -g appuser appuser

# Copy published application
COPY --from=publish /app/publish .

# Set ownership
RUN chown -R appuser:appuser /app

# Switch to non-root user
USER appuser

# Expose ports
EXPOSE 8080
EXPOSE 8081

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Entry point
ENTRYPOINT ["dotnet", "ControlPlane.Api.dll"]
