# Build context: Sources/ (docker build -f Sources/WebApis/Dockerfile Sources)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Domain/Domain.csproj Domain/
COPY Application/Application.csproj Application/
COPY Infrastructure/Infrastructure.csproj Infrastructure/
COPY WebApis/WebApis.csproj WebApis/
RUN dotnet restore WebApis/WebApis.csproj
COPY . .
RUN dotnet publish WebApis/WebApis.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "WebApis.dll"]
