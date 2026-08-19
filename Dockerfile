FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY SocketStudy.slnx ./
COPY SocketStudy/SocketStudy.csproj SocketStudy/
COPY SocketStudy.ProtocolTests/SocketStudy.ProtocolTests.csproj SocketStudy.ProtocolTests/
RUN dotnet restore SocketStudy.slnx
COPY . .
RUN dotnet publish SocketStudy/SocketStudy.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app .
VOLUME ["/app/Data", "/app/logs"]
EXPOSE 5000
ENTRYPOINT ["dotnet", "SocketStudy.dll", "server", "5000"]
