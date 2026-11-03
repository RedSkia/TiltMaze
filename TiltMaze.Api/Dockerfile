# Byg API'et med .NET SDK. Bygges fra roden af repoet, fordi API'et bruger TiltMaze.Core
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY TiltMaze.Core/TiltMaze.Core.csproj TiltMaze.Core/
COPY TiltMaze.Api/TiltMaze.Api.csproj TiltMaze.Api/
RUN dotnet restore TiltMaze.Api/TiltMaze.Api.csproj
COPY TiltMaze.Core TiltMaze.Core
COPY TiltMaze.Api TiltMaze.Api
RUN dotnet publish TiltMaze.Api/TiltMaze.Api.csproj -c Release -o /app --no-restore

# Kør API'et i et lille runtime-image som almindelig bruger (ikke root)
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
RUN mkdir /data && chown app /data
COPY --from=build /app .
USER app
ENTRYPOINT ["dotnet", "TiltMaze.Api.dll"]
