FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first so the layer is cached until a project file changes.
COPY OpenIron.sln .
COPY src/OpenIron.Api/OpenIron.Api.csproj src/OpenIron.Api/
COPY tests/OpenIron.Api.Tests/OpenIron.Api.Tests.csproj tests/OpenIron.Api.Tests/
RUN dotnet restore

COPY . .
RUN dotnet publish src/OpenIron.Api -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "OpenIron.Api.dll"]
