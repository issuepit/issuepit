FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src
COPY ["IssuePit.Notes.Api/IssuePit.Notes.Api.csproj", "IssuePit.Notes.Api/"]
COPY ["IssuePit.Notes.Core/IssuePit.Notes.Core.csproj", "IssuePit.Notes.Core/"]
COPY ["IssuePit.ServiceDefaults/IssuePit.ServiceDefaults.csproj", "IssuePit.ServiceDefaults/"]
RUN dotnet restore "IssuePit.Notes.Api/IssuePit.Notes.Api.csproj"
COPY IssuePit.Notes.Api/ IssuePit.Notes.Api/
COPY IssuePit.Notes.Core/ IssuePit.Notes.Core/
COPY IssuePit.ServiceDefaults/ IssuePit.ServiceDefaults/
WORKDIR /src/IssuePit.Notes.Api
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "IssuePit.Notes.Api.dll"]
