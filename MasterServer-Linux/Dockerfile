FROM mcr.microsoft.com/dotnet/runtime:7.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["MasterServer-Linux/MasterServer-Linux.csproj", "MasterServer-Linux/"]
RUN dotnet restore "MasterServer-Linux/MasterServer-Linux.csproj"
COPY . .
WORKDIR "/src/MasterServer-Linux"
RUN dotnet build "MasterServer-Linux.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MasterServer-Linux.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MasterServer-Linux.dll"]
