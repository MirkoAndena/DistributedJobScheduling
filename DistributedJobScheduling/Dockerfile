FROM mcr.microsoft.com/dotnet/runtime:3.1 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:3.1 AS build
WORKDIR /src
COPY ["DistributedJobScheduling/DistributedJobScheduling.csproj", "DistributedJobScheduling/"]
RUN dotnet restore "DistributedJobScheduling/DistributedJobScheduling.csproj"
COPY . .
WORKDIR "/src/DistributedJobScheduling"
RUN dotnet build "DistributedJobScheduling.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DistributedJobScheduling.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DistributedJobScheduling.dll"]
