#!/bin/bash
# Скрипт проверки health check после деплоя
# Использование: ./check_health.sh [URL]

URL=${1:-"http://localhost:8080"}

echo "Проверка health check после деплоя"
echo "Целевой URL: $URL"

# Функция для проверки эндпоинта
check_endpoint() {
    local endpoint="$1"
    local name="$2"
    
    if curl -f -s --max-time 10 "$endpoint" > /dev/null 2>&1; then
        echo "✅ $name: УСПЕХ"
        return 0
    else
        echo "❌ $name: ОШИБКА"
        return 1
    fi
}

# Проверка основных эндпоинтов
endpoints=(
    "/health:Overall Health"
    "/health/ready:Readiness"
    "/health/live:Liveness"
    "/metrics:Metrics"
    "/api/categories:API Categories"
)

all_success=true

for ep in "${endpoints[@]}"; do
    path="${ep%%:*}"
    name="${ep##*:}"
    full_url="${URL}${path}"
    
    if check_endpoint "$full_url" "$name"; then
        # Дополнительная информация для health endpoint
        if [ "$path" = "/health" ]; then
            health_data=$(curl -s "$full_url")
            status=$(echo "$health_data" | jq -r '.status' 2>/dev/null || echo "unknown")
            duration=$(echo "$health_data" | jq -r '.totalDuration' 2>/dev/null || echo "unknown")
            echo "   Статус: $status, Время ответа: ${duration}мс"
        fi
    else
        all_success=false
    fi
    
    sleep 0.2
done

echo ""
printf '=%.0s' {1..50}
echo ""

if $all_success; then
    echo "✅ ВСЕ ПРОВЕРКИ ПРОЙДЕНЫ УСПЕШНО!"
    echo "   Приложение готово к работе в production."
    exit 0
else
    echo "❌ НЕКОТОРЫЕ ПРОВЕРКИ НЕ ПРОШЛИ!"
    echo "   Проверьте логи приложения и настройки."
    exit 1
fi