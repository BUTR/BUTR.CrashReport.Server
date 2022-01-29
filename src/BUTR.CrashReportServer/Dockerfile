FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine AS build
WORKDIR /build

COPY ["src/BUTR.CrashReportServer/BUTR.CrashReportServer.csproj", "src/BUTR.CrashReportServer/"]
RUN dotnet restore "src/BUTR.CrashReportServer/BUTR.CrashReportServer.csproj"

COPY ["src/BUTR.CrashReportServer/", "src/BUTR.CrashReportServer/"]

RUN /bin/ash -c 'set -ex && \
    ARCH=`uname -m` && \
    if [[ "$ARCH" == "amd64" || "$ARCH" == "x86_64" ]]; then \
       echo "x86_64" && \
       dotnet publish "src/BUTR.CrashReportServer/BUTR.CrashReportServer.csproj" -c Release -o ./output -r alpine-x64 --self-contained true /p:PublishReadyToRun=true /p:PublishReadyToRunShowWarnings=true; \
    elif [[ "$ARCH" == "aarch64" || "$ARCH" == "arm64" ]]; then \
       echo "aarch64" && \
       dotnet publish "src/BUTR.CrashReportServer/BUTR.CrashReportServer.csproj" -c Release -o ./output -r alpine-arm64 --self-contained true; \
    else \
       echo "unknown arch" && \
       dotnet publish "src/BUTR.CrashReportServer/BUTR.CrashReportServer.csproj" -c Release -o ./output -r alpine --self-contained true; \
    fi'


FROM mcr.microsoft.com/dotnet/runtime-deps:6.0-alpine
WORKDIR /app
COPY --from=build /build/output .

LABEL org.opencontainers.image.source="https://github.com/BUTR/BUTR.CrashReportServer"
EXPOSE 80
ENTRYPOINT ["./BUTR.CrashReportServer"]
