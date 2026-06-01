# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY MemorySystem.sln ./
COPY src/MemorySystem.Api/MemorySystem.Api.csproj src/MemorySystem.Api/
COPY src/MemorySystem.Application/MemorySystem.Application.csproj src/MemorySystem.Application/
COPY src/MemorySystem.DemoSeeder/MemorySystem.DemoSeeder.csproj src/MemorySystem.DemoSeeder/
COPY src/MemorySystem.Domain/MemorySystem.Domain.csproj src/MemorySystem.Domain/
COPY src/MemorySystem.Infrastructure/MemorySystem.Infrastructure.csproj src/MemorySystem.Infrastructure/
COPY src/MemorySystem.Migrator/MemorySystem.Migrator.csproj src/MemorySystem.Migrator/
COPY src/MemorySystem.Worker/MemorySystem.Worker.csproj src/MemorySystem.Worker/
RUN dotnet restore MemorySystem.sln

COPY src/ src/
COPY migrations/ migrations/

RUN dotnet publish src/MemorySystem.Api/MemorySystem.Api.csproj --configuration Release --no-restore --output /out/api
RUN dotnet publish src/MemorySystem.Worker/MemorySystem.Worker.csproj --configuration Release --no-restore --output /out/worker
RUN dotnet publish src/MemorySystem.Migrator/MemorySystem.Migrator.csproj --configuration Release --no-restore --output /out/migrator
RUN dotnet publish src/MemorySystem.DemoSeeder/MemorySystem.DemoSeeder.csproj --configuration Release --no-restore --output /out/seeder

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends bash ca-certificates postgresql-client \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /out/api/ /app/api/
COPY --from=build /out/worker/ /app/worker/
COPY --from=build /out/migrator/ /app/migrator/
COPY --from=build /out/seeder/ /app/seeder/
COPY --from=build /src/migrations/ /app/migrations/
COPY scripts/platform-backup-export.sh /app/scripts/platform-backup-export.sh
COPY scripts/platform-erasure-replay-ledger-export.sh /app/scripts/platform-erasure-replay-ledger-export.sh
COPY scripts/platform-external-payload-retention-check.sh /app/scripts/platform-external-payload-retention-check.sh
COPY scripts/platform-retention-minimization.sh /app/scripts/platform-retention-minimization.sh
COPY scripts/platform-restore-validation.sh /app/scripts/platform-restore-validation.sh
COPY scripts/restore-validation-tables.sh /app/scripts/restore-validation-tables.sh
COPY scripts/restore-validation-tables.txt /app/scripts/restore-validation-tables.txt

RUN chmod +x /app/scripts/platform-backup-export.sh /app/scripts/platform-erasure-replay-ledger-export.sh /app/scripts/platform-external-payload-retention-check.sh /app/scripts/platform-retention-minimization.sh /app/scripts/platform-restore-validation.sh

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "/app/api/MemorySystem.Api.dll"]
