# syntax=docker/dockerfile:1

# Build context is the PARENT directory (kart-commerce/), not this repo alone - see
# docker-compose.yml's `build.context: ..`. This is required because Kart.Shared.ErrorHandling/
# Kart.Shared.Observability/Kart.Shared.Domain are consumed via ProjectReference to the sibling
# kart-shared checkout (no published NuGet feed exists yet - kart-shared's own README documents
# this as the interim consumption path). Once kart-shared publishes real NuGet packages, this
# reverts to a normal single-repo build context with PackageReferences instead.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY kart-search-service/KartSearchService.sln kart-search-service/
COPY kart-search-service/Directory.Build.props kart-search-service/
COPY kart-shared/Directory.Build.props kart-shared/
COPY kart-search-service/src/Api/Kart.Search.Api.csproj kart-search-service/src/Api/
COPY kart-search-service/src/Application/Kart.Search.Application.csproj kart-search-service/src/Application/
COPY kart-search-service/src/Domain/Kart.Search.Domain.csproj kart-search-service/src/Domain/
COPY kart-search-service/src/Infrastructure/Kart.Search.Infrastructure.csproj kart-search-service/src/Infrastructure/
COPY kart-search-service/tests/UnitTests/Kart.Search.UnitTests.csproj kart-search-service/tests/UnitTests/
COPY kart-search-service/tests/IntegrationTests/Kart.Search.IntegrationTests.csproj kart-search-service/tests/IntegrationTests/
COPY kart-search-service/tests/ContractTests/Kart.Search.ContractTests.csproj kart-search-service/tests/ContractTests/
COPY kart-shared/src/Kart.Shared.Domain/Kart.Shared.Domain.csproj kart-shared/src/Kart.Shared.Domain/
COPY kart-shared/src/Kart.Shared.ErrorHandling/Kart.Shared.ErrorHandling.csproj kart-shared/src/Kart.Shared.ErrorHandling/
COPY kart-shared/src/Kart.Shared.Observability/Kart.Shared.Observability.csproj kart-shared/src/Kart.Shared.Observability/
COPY kart-shared/src/Kart.Shared.Configuration/Kart.Shared.Configuration.csproj kart-shared/src/Kart.Shared.Configuration/
COPY kart-shared/src/Kart.Shared.Messaging/Kart.Shared.Messaging.csproj kart-shared/src/Kart.Shared.Messaging/
# The cache mount persists extracted NuGet packages under a stable id shared by every other
# kart-*-service Dockerfile, so restore stays fast (no re-download) even on a cache-miss here
# (e.g. after a .csproj change) as long as some other service's build already warmed it.
RUN --mount=type=cache,target=/root/.nuget/packages,id=nuget-packages \
    dotnet restore kart-search-service/src/Api/Kart.Search.Api.csproj

# Scoped to src/ + contracts/ from each repo instead of the previous whole-directory
# `COPY kart-search-service/ kart-search-service/` / `COPY kart-shared/ kart-shared/` -- those
# also pulled in tests/, README.md, kart-shared's own tests/ and docs, etc., so editing any of
# that busted this layer (and the publish below) even though none of it reaches the published
# output. contracts/ is kept because Kart.Search.Api.csproj copies message-bus-manifest.json and
# api-contract.yaml from it into the publish output as <Content> items.
COPY kart-search-service/src/ kart-search-service/src/
COPY kart-search-service/contracts/ kart-search-service/contracts/
COPY kart-shared/src/ kart-shared/src/
# Deliberately no --no-restore (predates this change, left as-is): publish here performs its own
# restore, so it needs the same cache mount as the restore step above.
RUN --mount=type=cache,target=/root/.nuget/packages,id=nuget-packages \
    dotnet publish kart-search-service/src/Api/Kart.Search.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Kart.Search.Api.dll"]
