# Multi-stage build; PROJECT selects the host to publish:
#   podman build -t localhost/campaignmanager-api --build-arg PROJECT=CampaignManager.Api .
ARG PROJECT=CampaignManager.Api

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG PROJECT
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/ src/
RUN dotnet restore src/${PROJECT}/${PROJECT}.csproj
RUN dotnet publish src/${PROJECT}/${PROJECT}.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
ARG PROJECT
ENV APP_DLL=${PROJECT}.dll \
    ASPNETCORE_URLS=http://+:8080 \
    DataProtection__KeysPath=/keys
WORKDIR /app
COPY --from=build /app .
VOLUME /keys
EXPOSE 8080
ENTRYPOINT ["sh", "-c", "dotnet $APP_DLL"]
