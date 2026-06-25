FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
ARG TARGETARCH
WORKDIR /build

COPY ["src/BUTR.CrashReport.Server.Persistence/BUTR.CrashReport.Server.Persistence.csproj", "src/BUTR.CrashReport.Server.Persistence/"]
COPY ["src/BUTR.CrashReport.Server.v13/BUTR.CrashReport.Server.v13.csproj", "src/BUTR.CrashReport.Server.v13/"]
COPY ["src/BUTR.CrashReport.Server.v14/BUTR.CrashReport.Server.v14.csproj", "src/BUTR.CrashReport.Server.v14/"]
COPY ["src/BUTR.CrashReport.Server/BUTR.CrashReport.Server.csproj", "src/BUTR.CrashReport.Server/"]
COPY ["src/nuget.config", "src/"]

RUN dotnet restore "src/BUTR.CrashReport.Server/BUTR.CrashReport.Server.csproj" -a $TARGETARCH;

COPY ["src/BUTR.CrashReport.Server.Persistence/", "src/BUTR.CrashReport.Server.Persistence/"]
COPY ["src/BUTR.CrashReport.Server.v13/", "src/BUTR.CrashReport.Server.v13/"]
COPY ["src/BUTR.CrashReport.Server.v14/", "src/BUTR.CrashReport.Server.v14/"]
COPY ["src/BUTR.CrashReport.Server/", "src/BUTR.CrashReport.Server/"]

#COPY [".git/HEAD", ".git/HEAD"]
#COPY [".git/config", ".git/config"]
#COPY [".git/refs/heads/", ".git/refs/heads/"]


FROM restore AS publish
ARG TARGETARCH
WORKDIR /build

RUN dotnet publish "src/BUTR.CrashReport.Server/BUTR.CrashReport.Server.csproj" -c Release -a $TARGETARCH -o /app/publish;


# Chiseled (distroless) image: no shell or package manager, minimal attack surface, non-root by default.
# Plain (non -extra) variant is fine because InvariantGlobalization is enabled (no ICU) and the app is all-UTC (no tzdata).
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Disable the diagnostics IPC pipe in production to reduce attack surface.
ENV DOTNET_EnableDiagnostics=0

LABEL org.opencontainers.image.source="https://github.com/BUTR/BUTR.CrashReport.Server"
# Run as the image's pre-created non-root user (UID 1654).
USER $APP_UID
EXPOSE 8080/tcp
ENTRYPOINT ["dotnet", "BUTR.CrashReport.Server.dll"]