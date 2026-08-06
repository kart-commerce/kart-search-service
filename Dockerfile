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
RUN dotnet restore kart-search-service/src/Api/Kart.Search.Api.csproj

COPY kart-search-service/ kart-search-service/
COPY kart-shared/ kart-shared/
RUN dotnet publish kart-search-service/src/Api/Kart.Search.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Kart.Search.Api.dll"]
