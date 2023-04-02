FROM mcr.microsoft.com/dotnet/sdk:7.0-alpine AS build
WORKDIR /build

COPY ["src/BUTR.CrashReportServer/BUTR.CrashReportServer.csproj", "src/BUTR.CrashReportServer/"]
RUN dotnet restore "src/BUTR.CrashReportServer/BUTR.CrashReportServer.csproj"

COPY ["src/BUTR.CrashReportServer/", "src/BUTR.CrashReportServer/"]

RUN dotnet publish "src/BUTR.CrashReportServer/BUTR.CrashReportServer.csproj" -c Release -o ./output -r alpine-x64 --self-contained true /p:PublishReadyToRun=true /p:PublishReadyToRunShowWarnings=true;


FROM mcr.microsoft.com/dotnet/runtime-deps:7.0-alpine
WORKDIR /app
COPY --from=build /build/output .

LABEL org.opencontainers.image.source="https://github.com/BUTR/BUTR.CrashReportServer"
EXPOSE 80
ENTRYPOINT ["./BUTR.CrashReportServer"]
