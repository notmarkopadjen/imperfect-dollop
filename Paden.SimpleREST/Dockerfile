FROM mcr.microsoft.com/dotnet/core/aspnet:2.2-stretch-slim AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/core/sdk:2.2-stretch AS build
WORKDIR /src
COPY ["Paden.SimpleREST/Paden.SimpleREST.csproj", "Paden.SimpleREST/"]
RUN dotnet restore "Paden.SimpleREST/Paden.SimpleREST.csproj"
COPY . .
WORKDIR "/src/Paden.SimpleREST"
RUN dotnet build "Paden.SimpleREST.csproj" -c Release -o /app

FROM build AS publish
RUN dotnet publish "Paden.SimpleREST.csproj" -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "Paden.SimpleREST.dll"]