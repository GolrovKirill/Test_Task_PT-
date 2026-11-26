FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY .editorconfig ./
COPY src/PT.Api/PT.Api.csproj src/PT.Api/
COPY src/PT.Domain/PT.Domain.csproj src/PT.Domain/
COPY src/PT.Infrastructure/PT.Infrastructure.csproj src/PT.Infrastructure/

RUN dotnet restore src/PT.Api/PT.Api.csproj

COPY src/ src/

RUN dotnet publish src/PT.Api/PT.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

USER $APP_UID

ENTRYPOINT ["dotnet", "PT.Api.dll"]
