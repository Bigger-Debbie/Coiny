FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Coiny.API/Coiny.API.csproj Coiny.API/
RUN dotnet restore Coiny.API/Coiny.API.csproj
COPY Coiny.API/ Coiny.API/
RUN dotnet publish Coiny.API/Coiny.API.csproj --no-restore -c Release -o /out
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
RUN mkdir -p /data /home/app/.aspnet/DataProtection-Keys && chown -R $APP_UID /data /home/app
COPY --from=build /out .
COPY tools/testing/docker-test/start-api.sh /app/start-api.sh
USER $APP_UID
ENTRYPOINT ["sh", "/app/start-api.sh"]
