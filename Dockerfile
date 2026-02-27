FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["OMG.slnx", "./"]
COPY ["src/OMG.Api/OMG.Api.csproj", "src/OMG.Api/"]

RUN dotnet restore "src/OMG.Api/OMG.Api.csproj"

COPY . .

RUN dotnet publish "src/OMG.Api/OMG.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "OMG.Api.dll"]

