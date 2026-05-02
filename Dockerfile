# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY src/LogAnalyzer.Domain/LogAnalyzer.Domain.csproj src/LogAnalyzer.Domain/
COPY src/LogAnalyzer.Application/LogAnalyzer.Application.csproj src/LogAnalyzer.Application/
COPY src/LogAnalyzer.Infrastructure/LogAnalyzer.Infrastructure.csproj src/LogAnalyzer.Infrastructure/
COPY src/LogAnalyzer.Web/LogAnalyzer.Web.csproj src/LogAnalyzer.Web/

RUN dotnet restore src/LogAnalyzer.Web/LogAnalyzer.Web.csproj

COPY . .
RUN dotnet publish src/LogAnalyzer.Web/LogAnalyzer.Web.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends fontconfig fonts-dejavu-core \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "LogAnalyzer.Web.dll"]
