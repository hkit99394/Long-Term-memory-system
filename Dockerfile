# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:c0790639332692a0d56cdd81ed581cfd24d040d9839764c138994866df89a3b6 AS build
WORKDIR /src

ARG VERSION=1.0.0

COPY Directory.Build.props ./
COPY src/MemorySystem.Api/MemorySystem.Api.csproj src/MemorySystem.Api/
COPY src/MemorySystem.Application/MemorySystem.Application.csproj src/MemorySystem.Application/
COPY src/MemorySystem.DemoSeeder/MemorySystem.DemoSeeder.csproj src/MemorySystem.DemoSeeder/
COPY src/MemorySystem.Domain/MemorySystem.Domain.csproj src/MemorySystem.Domain/
COPY src/MemorySystem.Infrastructure/MemorySystem.Infrastructure.csproj src/MemorySystem.Infrastructure/
COPY src/MemorySystem.Migrator/MemorySystem.Migrator.csproj src/MemorySystem.Migrator/
COPY src/MemorySystem.Worker/MemorySystem.Worker.csproj src/MemorySystem.Worker/
RUN dotnet restore src/MemorySystem.Api/MemorySystem.Api.csproj
RUN dotnet restore src/MemorySystem.Worker/MemorySystem.Worker.csproj
RUN dotnet restore src/MemorySystem.Migrator/MemorySystem.Migrator.csproj
RUN dotnet restore src/MemorySystem.DemoSeeder/MemorySystem.DemoSeeder.csproj

COPY src/ src/
COPY docs/external-pilot-readiness-status.json docs/external-pilot-readiness-status.json
COPY docs/external-pilot-readiness-status.schema.json docs/external-pilot-readiness-status.schema.json
COPY migrations/ migrations/

RUN dotnet publish src/MemorySystem.Api/MemorySystem.Api.csproj --configuration Release --no-restore --output /out/api -p:UseAppHost=false -p:Version=$VERSION -p:InformationalVersion=$VERSION
RUN dotnet publish src/MemorySystem.Worker/MemorySystem.Worker.csproj --configuration Release --no-restore --output /out/worker -p:UseAppHost=false -p:Version=$VERSION -p:InformationalVersion=$VERSION
RUN dotnet publish src/MemorySystem.Migrator/MemorySystem.Migrator.csproj --configuration Release --no-restore --output /out/migrator -p:UseAppHost=false -p:Version=$VERSION -p:InformationalVersion=$VERSION
RUN dotnet publish src/MemorySystem.DemoSeeder/MemorySystem.DemoSeeder.csproj --configuration Release --no-restore --output /out/seeder -p:UseAppHost=false -p:Version=$VERSION -p:InformationalVersion=$VERSION

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:8c0b6857eab7b2aa57884c839bf4678414606bd7d17370f18a842ac5cf414711 AS runtime
WORKDIR /app

ARG APP_UID=60000
ARG VERSION=1.0.0
ARG SOURCE_REVISION=unknown
ARG BUILD_DATE=unknown

LABEL org.opencontainers.image.title="Long-Term Memory System" \
      org.opencontainers.image.description="Multi-role MemorySystem API, worker, migrator, seeder, and operator scripts." \
      org.opencontainers.image.version=$VERSION \
      org.opencontainers.image.revision=$SOURCE_REVISION \
      org.opencontainers.image.created=$BUILD_DATE

RUN apt-get update \
    && apt-get install -y --no-install-recommends bash ca-certificates curl postgresql-client \
    && rm -rf /var/lib/apt/lists/* \
    && groupadd --gid $APP_UID memorysystem \
    && useradd --uid $APP_UID --gid memorysystem --home-dir /app --shell /usr/sbin/nologin --no-create-home memorysystem

COPY --from=build --chown=memorysystem:memorysystem /out/api/ /app/api/
COPY --from=build --chown=memorysystem:memorysystem /out/worker/ /app/worker/
COPY --from=build --chown=memorysystem:memorysystem /out/migrator/ /app/migrator/
COPY --from=build --chown=memorysystem:memorysystem /out/seeder/ /app/seeder/
COPY --from=build --chown=memorysystem:memorysystem /src/migrations/ /app/migrations/
COPY --chown=memorysystem:memorysystem scripts/platform-backup-export.sh /app/scripts/platform-backup-export.sh
COPY --chown=memorysystem:memorysystem scripts/platform-compliance-evidence-package.sh /app/scripts/platform-compliance-evidence-package.sh
COPY --chown=memorysystem:memorysystem scripts/platform-erasure-replay-ledger-export.sh /app/scripts/platform-erasure-replay-ledger-export.sh
COPY --chown=memorysystem:memorysystem scripts/platform-external-payload-retention-check.sh /app/scripts/platform-external-payload-retention-check.sh
COPY --chown=memorysystem:memorysystem scripts/platform-retention-minimization.sh /app/scripts/platform-retention-minimization.sh
COPY --chown=memorysystem:memorysystem scripts/platform-restore-validation.sh /app/scripts/platform-restore-validation.sh
COPY --chown=memorysystem:memorysystem scripts/restore-validation-tables.sh /app/scripts/restore-validation-tables.sh
COPY --chown=memorysystem:memorysystem scripts/restore-validation-tables.txt /app/scripts/restore-validation-tables.txt

RUN chmod +x /app/scripts/platform-backup-export.sh \
    /app/scripts/platform-compliance-evidence-package.sh \
    /app/scripts/platform-erasure-replay-ledger-export.sh \
    /app/scripts/platform-external-payload-retention-check.sh \
    /app/scripts/platform-retention-minimization.sh \
    /app/scripts/platform-restore-validation.sh \
    /app/scripts/restore-validation-tables.sh

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0 \
    MEMORYSYSTEM_SERVICE_VERSION=$VERSION
EXPOSE 8080

USER memorysystem

CMD ["dotnet", "/app/api/MemorySystem.Api.dll"]
