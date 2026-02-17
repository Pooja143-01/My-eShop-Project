# -------- Stage 1: Build --------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Copy solution (helps caching)
COPY ./eShopOnWeb.sln ./

# Copy ONLY the projects we keep during container build
COPY ./src/ApplicationCore/ ./src/ApplicationCore/
COPY ./src/Infrastructure/   ./src/Infrastructure/
COPY ./src/Web/              ./src/Web/

# Provide TF + disable trimming only inside the container build
RUN printf '<Project>\n  <PropertyGroup>\n    <TargetFramework>net9.0</TargetFramework>\n    <PublishTrimmed>false</PublishTrimmed>\n  </PropertyGroup>\n</Project>\n' > Directory.Build.props

# Restore & publish Web with the switch that skips Admin/Aspire
RUN dotnet restore  ./src/Web/Web.csproj -p:TargetFramework=net9.0 -p:PublishTrimmed=false -p:BuildingContainer=true
RUN dotnet publish ./src/Web/Web.csproj -c Release -o /app/publish --no-restore -p:TargetFramework=net9.0 -p:PublishTrimmed=false -p:BuildingContainer=true

# -------- Stage 2: Runtime --------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Web.dll"]