FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
RUN adduser -u 1000 --disabled-password --gecos "" appuser

FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
ARG CONFIGURATION=Release
WORKDIR /src

COPY ["global.json", "."]
COPY ["Directory.Build.props", "."]
COPY ["src/Sinch.MessageRouter.Core/Sinch.MessageRouter.Core.csproj", "src/Sinch.MessageRouter.Core/"]
COPY ["src/Sinch.MessageRouter.Channels/Sinch.MessageRouter.Channels.csproj", "src/Sinch.MessageRouter.Channels/"]
COPY ["src/Sinch.MessageRouter.Webhooks/Sinch.MessageRouter.Webhooks.csproj", "src/Sinch.MessageRouter.Webhooks/"]
COPY ["src/Sinch.MessageRouter.Gateway/Sinch.MessageRouter.Gateway.csproj", "src/Sinch.MessageRouter.Gateway/"]
RUN dotnet restore "src/Sinch.MessageRouter.Gateway/Sinch.MessageRouter.Gateway.csproj"

COPY . .
WORKDIR "/src/src/Sinch.MessageRouter.Gateway"
RUN dotnet build -c $CONFIGURATION -o /app/build --no-restore

FROM build AS publish
ARG CONFIGURATION=Release
RUN dotnet publish -c $CONFIGURATION -o /app/publish --no-restore /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
USER appuser
HEALTHCHECK --interval=15s --timeout=5s --retries=3 CMD wget -qO- http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "Sinch.MessageRouter.Gateway.dll"]
