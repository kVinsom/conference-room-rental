FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY .editorconfig global.json Directory.Build.props Directory.Packages.props ./
COPY src/ConferenceRoomRental.Domain/ConferenceRoomRental.Domain.csproj src/ConferenceRoomRental.Domain/
COPY src/ConferenceRoomRental.Application/ConferenceRoomRental.Application.csproj src/ConferenceRoomRental.Application/
COPY src/ConferenceRoomRental.Infrastructure/ConferenceRoomRental.Infrastructure.csproj src/ConferenceRoomRental.Infrastructure/
COPY src/ConferenceRoomRental.Api/ConferenceRoomRental.Api.csproj src/ConferenceRoomRental.Api/
RUN dotnet restore src/ConferenceRoomRental.Api/ConferenceRoomRental.Api.csproj

COPY src/ src/
RUN dotnet publish src/ConferenceRoomRental.Api/ConferenceRoomRental.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080

COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "ConferenceRoomRental.Api.dll"]
