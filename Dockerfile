# Tendaa — single-container deploy: the React dashboard builds into Tendaa.Web's wwwroot,
# then Kestrel serves the API + SignalR + WhatsApp webhook. ffmpeg is needed in the final
# image for voice-note conversion (Phase 4a).

# Stage 1: dashboard SPA. Vite's outDir is ../src/Tendaa.Web/wwwroot, so this stage
# reproduces that layout and hands the folder to the dotnet stage.
FROM node:24 AS web-build
WORKDIR /build
COPY web/package.json web/package-lock.json ./web/
RUN cd web && npm ci
COPY web/ ./web/
RUN cd web && npm run build

# Stage 2: .NET publish.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet-build
WORKDIR /src
COPY . .
COPY --from=web-build /build/src/Tendaa.Web/wwwroot ./src/Tendaa.Web/wwwroot
RUN dotnet publish src/Tendaa.Web/Tendaa.Web.csproj -c Release -o /app/publish

# Stage 3: runtime (Debian-based ASP.NET image + ffmpeg).
FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=dotnet-build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Tendaa.Web.dll"]
