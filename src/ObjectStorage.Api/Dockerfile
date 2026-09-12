# Build from the REPO ROOT so referenced src/* projects are in context:
#   docker build -f src/ObjectStorage.Api/Dockerfile .
# (docker/docker-compose.yml already sets context: .. for this reason.)
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

COPY ObjectStorage.slnx ./
COPY src/ObjectStorage.Api/ObjectStorage.Api.csproj src/ObjectStorage.Api/
COPY src/ObjectStorage.Application/ObjectStorage.Application.csproj src/ObjectStorage.Application/
COPY src/ObjectStorage.Domain/ObjectStorage.Domain.csproj src/ObjectStorage.Domain/
COPY src/ObjectStorage.Infrastructure/ObjectStorage.Infrastructure.csproj src/ObjectStorage.Infrastructure/
RUN dotnet restore src/ObjectStorage.Api/ObjectStorage.Api.csproj

COPY src/ src/
RUN dotnet publish src/ObjectStorage.Api/ObjectStorage.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .

# Non-root: aspnet-alpine images ship an 'app' user.
USER app

# busybox wget ships with alpine — no extra curl layer needed.
HEALTHCHECK --interval=10s --timeout=3s --start-period=10s --retries=3 \
  CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "ObjectStorage.Api.dll"]
