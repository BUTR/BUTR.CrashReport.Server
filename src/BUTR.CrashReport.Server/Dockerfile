FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0-jammy AS restore
ARG TARGETARCH
WORKDIR /build

COPY ["src/BUTR.CrashReport.Server.Base/BUTR.CrashReport.Server.Base.csproj", "src/BUTR.CrashReport.Server.Base/"]
COPY ["src/BUTR.CrashReport.Server.v13/BUTR.CrashReport.Server.v13.csproj", "src/BUTR.CrashReport.Server.v13/"]
COPY ["src/BUTR.CrashReport.Server.v14/BUTR.CrashReport.Server.v14.csproj", "src/BUTR.CrashReport.Server.v14/"]
COPY ["src/BUTR.CrashReport.Server/BUTR.CrashReport.Server.csproj", "src/BUTR.CrashReport.Server/"]
COPY ["src/nuget.config", "src/"]

RUN dotnet restore "src/BUTR.CrashReport.Server/BUTR.CrashReport.Server.csproj" -a $TARGETARCH;

COPY ["src/BUTR.CrashReport.Server.Base/", "src/BUTR.CrashReport.Server.Base/"]
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


FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy AS final
WORKDIR /app
COPY --from=publish /app/publish .

LABEL org.opencontainers.image.source="https://github.com/BUTR/BUTR.CrashReport.Server"
EXPOSE 8080/tcp
ENTRYPOINT ["dotnet", "BUTR.CrashReport.Server.dll"]