# Build context is the Project/ directory (see docker-compose.yml: context: .)
# so this stage can reach the sibling MyBudget.Features project reference.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, in its own layer, so dependency changes don't bust the build cache
COPY src/MyBudget.Api/MyBudget.Api.csproj src/MyBudget.Api/
COPY src/MyBudget.Features/MyBudget.Features.csproj src/MyBudget.Features/
RUN dotnet restore src/MyBudget.Api/MyBudget.Api.csproj

COPY src/MyBudget.Api/ src/MyBudget.Api/
COPY src/MyBudget.Features/ src/MyBudget.Features/

RUN dotnet publish src/MyBudget.Api/MyBudget.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN useradd --user-group --no-create-home appuser
COPY --from=build /app/publish .
USER appuser

EXPOSE 8080
ENTRYPOINT ["dotnet", "MyBudget.Api.dll"]
