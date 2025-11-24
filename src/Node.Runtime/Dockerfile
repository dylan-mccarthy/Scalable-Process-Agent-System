# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["BusinessProcessAgents.sln", "./"]
COPY ["src/Node.Runtime/Node.Runtime.csproj", "src/Node.Runtime/"]
COPY ["src/ControlPlane.Api/ControlPlane.Api.csproj", "src/ControlPlane.Api/"]

# Restore dependencies
RUN dotnet restore "src/Node.Runtime/Node.Runtime.csproj"

# Copy source code
COPY ["src/Node.Runtime/", "src/Node.Runtime/"]
COPY ["src/ControlPlane.Api/Protos/", "src/ControlPlane.Api/Protos/"]

# Build the application
WORKDIR "/src/src/Node.Runtime"
RUN dotnet build "Node.Runtime.csproj" -c Release -o /app/build --no-restore

# Publish stage
FROM build AS publish
RUN dotnet publish "Node.Runtime.csproj" -c Release -o /app/publish /p:UseAppHost=false --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Create non-root user
RUN groupadd -r appuser && useradd -r -g appuser appuser

# Copy published application
COPY --from=publish /app/publish .

# Set ownership
RUN chown -R appuser:appuser /app

# Switch to non-root user
USER appuser

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# Entry point
ENTRYPOINT ["dotnet", "Node.Runtime.dll"]
