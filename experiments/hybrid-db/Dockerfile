FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Lab/Lab.csproj Lab/
RUN dotnet restore Lab/Lab.csproj
COPY Lab/ Lab/
RUN dotnet publish Lab/Lab.csproj --no-restore -c Release -o /out
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
RUN mkdir -p /data/households /data/backups && chown -R $APP_UID /data
COPY --from=build /out .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Lab.dll"]
