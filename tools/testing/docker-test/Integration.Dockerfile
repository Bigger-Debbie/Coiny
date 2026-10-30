FROM mcr.microsoft.com/dotnet/sdk:10.0-noble
# Official PostgreSQL repository supplies a client matching the test server major.
RUN apt-get update && apt-get install -y --no-install-recommends curl ca-certificates \
    && install -d /usr/share/postgresql-common/pgdg \
    && curl --fail --silent --show-error https://www.postgresql.org/media/keys/ACCC4CF8.asc -o /usr/share/postgresql-common/pgdg/apt.postgresql.org.asc \
    && echo "deb [signed-by=/usr/share/postgresql-common/pgdg/apt.postgresql.org.asc] https://apt.postgresql.org/pub/repos/apt noble-pgdg main" > /etc/apt/sources.list.d/pgdg.list \
    && apt-get update && apt-get install -y --no-install-recommends postgresql-client-17 age python3 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /src
COPY Coiny.API/Coiny.API.csproj Coiny.API/
COPY Coiny.API.Tests/Coiny.API.Tests.csproj Coiny.API.Tests/
RUN dotnet restore Coiny.API.Tests/Coiny.API.Tests.csproj
COPY Coiny.API/ Coiny.API/
COPY Coiny.API.Tests/ Coiny.API.Tests/
COPY tools/operations/ /operations/
RUN python3 -B -m unittest discover -s /operations -v
RUN dotnet build Coiny.API.Tests/Coiny.API.Tests.csproj --no-restore
ENTRYPOINT ["dotnet", "test", "Coiny.API.Tests/Coiny.API.Tests.csproj", "--no-build", "--no-restore", "--filter", "FullyQualifiedName~HybridStorageIntegrationTests|FullyQualifiedName~HybridApiIntegrationTests|FullyQualifiedName~HybridRecoveryIntegrationTests|FullyQualifiedName~HybridMaintenanceIntegrationTests|FullyQualifiedName~BackupEncryptionTests|FullyQualifiedName~HybridHouseholdRecoveryIntegrationTests", "--logger", "console;verbosity=normal"]
