# The API image. Build it from the project root (docker-compose.yml does):
#   docker build -f src/Scriptorium.API/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# The project files first, so the packages are restored again only when they change.
COPY src/Scriptorium.Core/Scriptorium.Core.csproj src/Scriptorium.Core/
COPY src/Scriptorium.Infrastructure/Scriptorium.Infrastructure.csproj src/Scriptorium.Infrastructure/
COPY src/Scriptorium.API/Scriptorium.API.csproj src/Scriptorium.API/
RUN dotnet restore src/Scriptorium.API/Scriptorium.API.csproj

COPY src/Scriptorium.Core/ src/Scriptorium.Core/
COPY src/Scriptorium.Infrastructure/ src/Scriptorium.Infrastructure/
COPY src/Scriptorium.API/ src/Scriptorium.API/
RUN dotnet publish src/Scriptorium.API/Scriptorium.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# The database and the uploaded files live in /data, which docker-compose.yml keeps in a volume.
# Created and given to the non-root user here, so a new volume starts with the right owner.
RUN mkdir /data && chown app:app /data
USER app

# The port is set by docker-compose.yml (ASPNETCORE_URLS); the API is not published on the computer.
EXPOSE 8080
ENTRYPOINT ["dotnet", "Scriptorium.API.dll"]
