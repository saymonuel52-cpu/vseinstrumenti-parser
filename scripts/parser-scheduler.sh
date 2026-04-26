#!/bin/sh
# Скрипт планировщика для периодического парсинга
# Запускается через cron внутри контейнера parser-scheduler

set -e

# URL приложения парсера (из переменных окружения)
PARSER_URL="${PARSER_URL:-http://vseinstrumenti-parser:8080}"
HEALTH_CHECK_URL="${HEALTH_CHECK_URL:-${PARSER_URL}/health}"
CRON_SCHEDULE="${CRON_SCHEDULE:-0 */6 * * *}"  # По умолчанию каждые 6 часов

# Функция логирования
log() {
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] $1"
}

# Функция проверки здоровья приложения
check_health() {
    if curl -f -s --max-time 10 "$HEALTH_CHECK_URL" > /dev/null 2>&1; then
        log "Health check passed"
        return 0
    else
        log "Health check failed"
        return 1
    fi
}

# Функция запуска парсинга
run_parsing() {
    local endpoint="$1"
    log "Starting parsing via ${endpoint}"
    
    # Выполняем HTTP запрос к API парсера
    response=$(curl -s -w "%{http_code}" -o /tmp/parsing_response.json \
        -X POST \
        -H "Content-Type: application/json" \
        -d '{"categories": ["all"], "maxProducts": 100, "exportFormat": "JSON"}' \
        "${endpoint}" 2>/dev/null)
    
    status_code="${response: -3}"
    response_body="$(cat /tmp/parsing_response.json 2>/dev/null || echo '{}')"
    
    if [ "$status_code" = "200" ] || [ "$status_code" = "202" ]; then
        log "Parsing started successfully (HTTP $status_code)"
        log "Response: $response_body"
        return 0
    else
        log "Parsing failed (HTTP $status_code)"
        log "Response: $response_body"
        return 1
    fi
}

# Основная функция
main() {
    log "Parser scheduler started"
    log "Parser URL: $PARSER_URL"
    log "Health check URL: $HEALTH_CHECK_URL"
    log "Cron schedule: $CRON_SCHEDULE"
    
    # Проверяем, что приложение доступно
    if ! check_health; then
        log "Application is not healthy, waiting 30 seconds and retrying..."
        sleep 30
        if ! check_health; then
            log "Application still unhealthy, aborting scheduled parsing"
            exit 1
        fi
    fi
    
    # Запускаем парсинг
    run_parsing "${PARSER_URL}/api/parse"
    
    log "Parsing job completed"
}

# Если скрипт вызван напрямую (не через cron), выполняем main
if [ "$1" = "--run-now" ]; then
    main
    exit $?
fi

# Иначе настраиваем cron
log "Setting up cron job with schedule: $CRON_SCHEDULE"

# Создаем файл crontab
echo "$CRON_SCHEDULE /usr/local/bin/parser-scheduler.sh --run-now >> /var/log/cron.log 2>&1" > /etc/crontabs/root
echo "# Empty line" >> /etc/crontabs/root

# Запускаем cron в foreground
log "Starting cron daemon..."
exec crond -f -l 8