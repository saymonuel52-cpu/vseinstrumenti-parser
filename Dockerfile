# Многоступенчатый Dockerfile для VseinstrumentiParser
# Используем .NET 8.0 SDK для сборки
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Копируем файлы проекта и восстанавливаем зависимости
COPY VseinstrumentiParser.csproj .
RUN dotnet restore

# Копируем весь исходный код
COPY . .

# Сборка проекта в режиме Release
RUN dotnet publish -c Release -o /app/publish

# Финальный образ с runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Устанавливаем необходимые системные пакеты для работы с кэшированием и мониторингом
RUN apt-get update && apt-get install -y \
    curl \
    iputils-ping \
    && rm -rf /var/lib/apt/lists/*

# Копируем собранное приложение
COPY --from=build /app/publish .

# Создаем директории для логов, экспортов и данных
RUN mkdir -p /app/logs /app/exports /app/data /app/monitoring

# Настраиваем переменные окружения
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV TZ=Europe/Moscow

# Открываем порт для Health Checks (если будет веб-хост)
EXPOSE 8080

# Точка входа
ENTRYPOINT ["dotnet", "VseinstrumentiParser.dll"]