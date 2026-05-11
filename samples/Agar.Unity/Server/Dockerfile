# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/runtime:10.0.7 AS runtime
WORKDIR /app
ENV DOTNET_EnableDiagnostics=0

FROM mcr.microsoft.com/dotnet/sdk:10.0.203 AS build
WORKDIR /src

COPY NuGet.config Directory.Build.props ./
COPY src/ULinkGame.Server/ULinkGame.Server.csproj src/ULinkGame.Server/
COPY samples/Agar.Unity/Shared/Shared.csproj samples/Agar.Unity/Shared/
COPY samples/Agar.Unity/Shared/Directory.Build.props samples/Agar.Unity/Shared/
COPY samples/Agar.Unity/Server/Orleans.Contracts/Orleans.Contracts.csproj samples/Agar.Unity/Server/Orleans.Contracts/
COPY samples/Agar.Unity/Server/Edge/Edge.csproj samples/Agar.Unity/Server/Edge/
COPY samples/Agar.Unity/Server/Silo/Silo.csproj samples/Agar.Unity/Server/Silo/

RUN dotnet restore samples/Agar.Unity/Server/Edge/Edge.csproj
RUN dotnet restore samples/Agar.Unity/Server/Silo/Silo.csproj

COPY src/ULinkGame.Server/ src/ULinkGame.Server/
COPY samples/Agar.Unity/Shared/ samples/Agar.Unity/Shared/
COPY samples/Agar.Unity/Server/ samples/Agar.Unity/Server/

FROM build AS publish-edge
RUN dotnet publish samples/Agar.Unity/Server/Edge/Edge.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish/edge

FROM build AS publish-silo
RUN dotnet publish samples/Agar.Unity/Server/Silo/Silo.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish/silo

FROM runtime AS edge
COPY --from=publish-edge /app/publish/edge ./
EXPOSE 20000 20001
USER $APP_UID
ENTRYPOINT ["dotnet", "Edge.dll"]

FROM runtime AS silo
COPY --from=publish-silo /app/publish/silo ./
EXPOSE 11111 30000
USER $APP_UID
ENTRYPOINT ["dotnet", "Silo.dll"]
