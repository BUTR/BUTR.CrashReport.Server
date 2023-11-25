FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["BUTR.CrashReportServer/BUTR.CrashReportServer.csproj", "BUTR.CrashReportServer/"]

RUN dotnet restore "BUTR.CrashReportServer/BUTR.CrashReportServer.csproj"

COPY . .

WORKDIR "/src/BUTR.CrashReportServer"
RUN dotnet build "BUTR.CrashReportServer.csproj" -c Release -o /app/build


FROM build AS publish
RUN dotnet publish "BUTR.CrashReportServer.csproj" -c Release -o /app/publish


FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

LABEL org.opencontainers.image.source="https://github.com/BUTR/BUTR.CrashReportServer"
EXPOSE 80/tcp
ENTRYPOINT ["dotnet", "BUTR.CrashReportServer.dll"]