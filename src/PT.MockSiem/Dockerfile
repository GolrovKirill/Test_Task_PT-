FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY .editorconfig ./
COPY src/PT.MockSiem/PT.MockSiem.csproj src/PT.MockSiem/

RUN dotnet restore src/PT.MockSiem/PT.MockSiem.csproj

COPY src/PT.MockSiem/ src/PT.MockSiem/

RUN dotnet publish src/PT.MockSiem/PT.MockSiem.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

USER $APP_UID

ENTRYPOINT ["dotnet", "PT.MockSiem.dll"]
