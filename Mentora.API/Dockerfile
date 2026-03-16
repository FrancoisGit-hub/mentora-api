FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["Mentora.API/Mentora.API.csproj", "Mentora.API/"]
COPY ["Mentora.Core/Mentora.Core.csproj", "Mentora.Core/"]
COPY ["Mentora.Infrastructure/Mentora.Infrastructure.csproj", "Mentora.Infrastructure/"]

RUN dotnet restore "Mentora.API/Mentora.API.csproj"

COPY . .

RUN dotnet build "Mentora.API/Mentora.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Mentora.API/Mentora.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Mentora.API.dll"]