# 1) Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0.11-alpine3.23 AS base
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8111
# Enable full globalization support (ICU) for culture-sensitive components
RUN apk add --no-cache icu-libs
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=0
EXPOSE 8111 8111

# 2) Build
FROM mcr.microsoft.com/dotnet/sdk:10.0.400-alpine3.23 AS build
WORKDIR /src
ARG BUILD_CONFIGURATION=Release

# Copy global build inputs and only the Web dependency graph before restore.
COPY global.json ./
COPY src/HelpDesk.NewWeb/*.csproj          src/HelpDesk.NewWeb/
COPY src/Helpdesk.ServiceDefaults/*.csproj src/Helpdesk.ServiceDefaults/
COPY src/Helpdesk.Shared/*.csproj          src/Helpdesk.Shared/

RUN dotnet restore src/HelpDesk.NewWeb/HelpDesk.NewWeb.csproj

COPY src/HelpDesk.NewWeb/          src/HelpDesk.NewWeb/
COPY src/Helpdesk.ServiceDefaults/ src/Helpdesk.ServiceDefaults/
COPY src/Helpdesk.Shared/          src/Helpdesk.Shared/

# Publish the Web project
RUN dotnet publish src/HelpDesk.NewWeb/HelpDesk.NewWeb.csproj \
    -c $BUILD_CONFIGURATION -o /out --no-restore -v:n \
    /p:UseAppHost=false

# 3) Final
FROM base AS final
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "HelpDesk.NewWeb.dll"]
