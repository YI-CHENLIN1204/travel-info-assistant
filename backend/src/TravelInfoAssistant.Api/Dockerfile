FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json ./
COPY backend/Directory.Build.props backend/
COPY backend/src/TravelInfoAssistant.Api/TravelInfoAssistant.Api.csproj backend/src/TravelInfoAssistant.Api/
RUN dotnet restore backend/src/TravelInfoAssistant.Api/TravelInfoAssistant.Api.csproj

COPY backend backend
RUN dotnet publish backend/src/TravelInfoAssistant.Api/TravelInfoAssistant.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "TravelInfoAssistant.Api.dll"]
