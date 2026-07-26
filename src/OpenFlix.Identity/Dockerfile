FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/OpenFlix.Identity/OpenFlix.Identity.csproj -c Release -o /app
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
ENV DOTNET_EnableDiagnostics=0
USER $APP_UID
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "OpenFlix.Identity.dll"]
