FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY NocodeX.sln ./
COPY src/NocodeX.Cli/NocodeX.Cli.csproj src/NocodeX.Cli/
COPY src/NocodeX.Application/NocodeX.Application.csproj src/NocodeX.Application/
COPY src/NocodeX.Core/NocodeX.Core.csproj src/NocodeX.Core/
COPY src/NocodeX.Infrastructure/NocodeX.Infrastructure.csproj src/NocodeX.Infrastructure/

RUN dotnet restore NocodeX.sln

COPY src/ ./src/
RUN dotnet publish src/NocodeX.Cli/NocodeX.Cli.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /workspace

COPY --from=build /app/publish /opt/nocodex

ENTRYPOINT ["dotnet", "/opt/nocodex/NocodeX.Cli.dll"]
