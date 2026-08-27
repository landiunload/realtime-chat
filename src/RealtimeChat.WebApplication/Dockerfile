# Многоступенчатая сборка приложения чата
FROM mcr.microsoft.com/dotnet/sdk:10.0.400-alpine3.23 AS build-stage
WORKDIR /source
COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/RealtimeChat.WebApplication/RealtimeChat.WebApplication.csproj src/RealtimeChat.WebApplication/
COPY src/RealtimeChat.WebApplication/packages.lock.json src/RealtimeChat.WebApplication/
RUN dotnet restore src/RealtimeChat.WebApplication/RealtimeChat.WebApplication.csproj --locked-mode
COPY src/ src/
RUN dotnet publish src/RealtimeChat.WebApplication/RealtimeChat.WebApplication.csproj \
    --configuration Release --output /application --no-self-contained \
    -p:UseAppHost=false -p:DebugType=None -p:DebugSymbols=false --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0.11-alpine3.23 AS runtime-stage
WORKDIR /application
COPY --from=build-stage /application .
ENV DOTNET_EnableDiagnostics=0
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "RealtimeChat.WebApplication.dll"]
