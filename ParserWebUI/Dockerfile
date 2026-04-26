FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["ParserWebUI/ParserWebUI.csproj", "ParserWebUI/"]
COPY ["VseininstrumentiParser.csproj", "."]
RUN dotnet restore "ParserWebUI/ParserWebUI.csproj"
COPY . .
WORKDIR "/src/ParserWebUI"
RUN dotnet build "ParserWebUI.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ParserWebUI.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "VseinstrumentiParser.WebUI.dll"]
