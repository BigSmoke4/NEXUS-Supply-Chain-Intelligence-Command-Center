# Multi-stage build (§65). Build with:
#   docker build -t nexus-web .
# Run with:
#   docker run -p 8080:8080 --env ConnectionStrings__Nexus="Host=...;..." nexus-web
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore Nexus.sln
RUN dotnet publish src/Nexus.Web/Nexus.Web.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s CMD curl -f http://localhost:8080/health/live || exit 1
ENTRYPOINT ["dotnet", "Nexus.Web.dll"]
