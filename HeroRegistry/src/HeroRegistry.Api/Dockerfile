# Build context: the HeroRegistry solution folder (one level above src/).
#   docker build -f src/HeroRegistry.Api/Dockerfile -t hero-registry-api .

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
# Restore first (cached layer): build props and project files only.
COPY ["Directory.Build.props", "Directory.Packages.props", "./"]
COPY ["src/Directory.Build.props", "src/"]
COPY ["src/HeroRegistry.Domain/HeroRegistry.Domain.csproj", "src/HeroRegistry.Domain/"]
COPY ["src/HeroRegistry.Application/HeroRegistry.Application.csproj", "src/HeroRegistry.Application/"]
COPY ["src/HeroRegistry.Infrastructure/HeroRegistry.Infrastructure.csproj", "src/HeroRegistry.Infrastructure/"]
COPY ["src/HeroRegistry.Api/HeroRegistry.Api.csproj", "src/HeroRegistry.Api/"]
RUN dotnet restore "src/HeroRegistry.Api/HeroRegistry.Api.csproj"
COPY src/ src/
WORKDIR "/src/src/HeroRegistry.Api"
RUN dotnet build "HeroRegistry.Api.csproj" -c $BUILD_CONFIGURATION --no-restore -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "HeroRegistry.Api.csproj" -c $BUILD_CONFIGURATION --no-restore -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HeroRegistry.Api.dll"]
