FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY NocodeX.sln ./
COPY src/NocodeX.Cli/NocodeX.Cli.csproj src/NocodeX.Cli/
COPY src/NocodeX.ConfigWeb/NocodeX.ConfigWeb.csproj src/NocodeX.ConfigWeb/
COPY src/NocodeX.Application/NocodeX.Application.csproj src/NocodeX.Application/
COPY src/NocodeX.Core/NocodeX.Core.csproj src/NocodeX.Core/
COPY src/NocodeX.Infrastructure/NocodeX.Infrastructure.csproj src/NocodeX.Infrastructure/

RUN dotnet restore src/NocodeX.Cli/NocodeX.Cli.csproj
RUN dotnet restore src/NocodeX.ConfigWeb/NocodeX.ConfigWeb.csproj

COPY src/ ./src/
RUN dotnet publish src/NocodeX.Cli/NocodeX.Cli.csproj -c Release -o /app/publish --no-restore
RUN dotnet publish src/NocodeX.ConfigWeb/NocodeX.ConfigWeb.csproj -c Release -o /app/publish-web --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /workspace

RUN apt-get update \
    && apt-get install -y --no-install-recommends nodejs npm \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish /opt/nocodex
COPY --from=build /app/publish-web /opt/nocodex-web

ENTRYPOINT ["dotnet", "/opt/nocodex/NocodeX.Cli.dll"]
