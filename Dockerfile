# syntax=docker/dockerfile:1

# ---- Build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG CONFIGURATION=Release
ENV CI=true
WORKDIR /source

# Restore first (cached layer): only build configuration, project and lock files.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/HackerNews.BestStories.Api/HackerNews.BestStories.Api.csproj src/HackerNews.BestStories.Api/packages.lock.json src/HackerNews.BestStories.Api/
RUN dotnet restore src/HackerNews.BestStories.Api/HackerNews.BestStories.Api.csproj

COPY src/ src/
RUN dotnet publish src/HackerNews.BestStories.Api/HackerNews.BestStories.Api.csproj \
    --configuration $CONFIGURATION \
    --no-restore \
    --output /app \
    /p:UseAppHost=false

# ---- Runtime ----
# Chiseled image: minimal attack surface, no shell or package manager, runs as non-root by default.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS final
WORKDIR /app
COPY --from=build /app .

USER $APP_UID
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

ENTRYPOINT ["dotnet", "HackerNews.BestStories.Api.dll"]
