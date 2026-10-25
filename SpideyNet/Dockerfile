FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only the relay layers; the Windows game bridge stays on the host.
COPY Directory.Build.props Directory.Build.targets Directory.Packages.props NuGet.Config global.json ./
COPY Core/ Core/
COPY Transport/ Transport/
COPY Relay/ Relay/

# Override the Windows single-file defaults with a portable framework-dependent DLL.
RUN dotnet publish Relay/Relay.csproj -c Release -o /app -m:1 \
    -p:RuntimeIdentifier= -p:PublishSingleFile=false -p:UseAppHost=false --self-contained false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/ ./

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Relay.dll"]
