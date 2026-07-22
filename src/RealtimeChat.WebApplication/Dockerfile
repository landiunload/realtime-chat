# Многоступенчатая сборка приложения чата
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-stage
WORKDIR /source
COPY src/RealtimeChat.WebApplication/RealtimeChat.WebApplication.csproj src/RealtimeChat.WebApplication/
RUN dotnet restore src/RealtimeChat.WebApplication/RealtimeChat.WebApplication.csproj
COPY src/ src/
RUN dotnet publish src/RealtimeChat.WebApplication/RealtimeChat.WebApplication.csproj \
    --configuration Release --output /application --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime-stage
WORKDIR /application
COPY --from=build-stage /application .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "RealtimeChat.WebApplication.dll"]
