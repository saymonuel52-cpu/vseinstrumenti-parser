#!/bin/bash
#
# start.sh - Запуск vseinstrumenti-parser стека
# Linux/macOS Bash скрипт с автоматическим открытием веб-интерфейса
#

set -e

# Аргументы
REBUILD=false
NO_BROWSER=false
TIMEOUT=120

# Парсинг аргументов
while [[ $# -gt 0 ]]; do
    case $1 in
        --rebuild|-r)
            REBUILD=true
            shift
            ;;
        --no-browser|-n)
            NO_BROWSER=true
            shift
            ;;
        --timeout|-t)
            TIMEOUT="$2"
            shift 2
            ;;
        *)
            echo "Неизвестный параметр: $1"
            echo "Использование: $0 [--rebuild] [--no-browser] [--timeout SECONDS]"
            exit 1
            ;;
    esac
done

# Цвета для вывода
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Функции для красивого вывода
print_header() {
    echo -e "\n${BLUE}========================================${NC}"
    echo -e "${BLUE}  $1${NC}"
    echo -e "${BLUE}========================================${NC}\n"
}

print_success() {
    echo -e "${GREEN}✅ $1${NC}"
}

print_info() {
    echo -e "${CYAN}ℹ️   $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}⚠️   $1${NC}"
}

print_error() {
    echo -e "${RED}❌ $1${NC}"
}

# Проверка зависимостей
print_header "Проверка зависимостей"

# Проверка Docker
print_info "Проверка Docker..."
if command -v docker &> /dev/null; then
    DOCKER_VERSION=$(docker --version)
    print_success "Docker установлен: $DOCKER_VERSION"
else
    print_error "Docker не найден в PATH"
    echo -e "\nУстановите Docker:"
    echo "  Ubuntu/Debian: sudo apt-get install docker.io"
    echo "  macOS: brew install --cask docker"
    echo "  Fedora: sudo dnf install docker-ce"
    echo "  Или скачайте: https://www.docker.com/products/docker-desktop\n"
    exit 1
fi

# Проверка docker-compose
print_info "Проверка Docker Compose..."
if command -v docker-compose &> /dev/null; then
    COMPOSE_VERSION=$(docker-compose --version)
    print_success "Docker Compose установлен: $COMPOSE_VERSION"
elif docker compose version &> /dev/null; then
    COMPOSE_VERSION=$(docker compose version)
    print_success "Docker Compose (v2) установлен: $COMPOSE_VERSION"
    COMPOSE_CMD="docker compose"
else
    print_error "Docker Compose не найден"
    echo -e "\nУстановите Docker Compose:"
    echo "  https://docs.docker.com/compose/install/\n"
    exit 1
fi

# Устанавливаем команду docker-compose
if [ -z "$COMPOSE_CMD" ]; then
    COMPOSE_CMD="docker-compose"
fi

# Проверка .NET SDK (опционально)
print_info "Проверка .NET SDK..."
if command -v dotnet &> /dev/null; then
    DOTNET_VERSION=$(dotnet --version)
    print_success ".NET SDK установлен: $DOTNET_VERSION"
else
    print_warning ".NET SDK не найден (необязательно, если используете только Docker)"
fi

# Остановка старых контейнеров
print_header "Остановка старых сервисов"

print_info "Очистка старых контейнеров..."
if $COMPOSE_CMD down --remove-orphans --volumes > /dev/null 2>&1; then
    print_success "Старые контейнеры остановлены"
else
    print_warning "Не удалось остановить контейнеры (возможно, они не были запущены)"
fi

# Пересборка образов (если указано)
if [ "$REBUILD" = true ]; then
    print_header "Пересборка образов"
    print_info "Сборка Docker образов..."
    if $COMPOSE_CMD build --no-cache; then
        print_success "Образы пересобраны"
    else
        print_error "Ошибка при сборке образов"
        exit 1
    fi
fi

# Запуск стека
print_header "Запуск сервисов"

print_info "Запуск 8 сервисов (parser, webui, redis, prometheus, grafana, seq, otel, alertmanager)..."
if $COMPOSE_CMD up -d; then
    print_success "Контейнеры запущены в фоновом режиме"
else
    print_error "Ошибка при запуске контейнеров"
    echo -e "\nПроверьте логи: $COMPOSE_CMD logs -f"
    exit 1
fi

# Ожидание готовности
print_header "Ожидание готовности сервисов"

HEALTH_URL="http://localhost:8080/health/ready"
MAX_ATTEMPTS=$((TIMEOUT / 3))
ATTEMPT=0
READY=false

print_info "Ожидание запуска Web UI ($TIMEOUT секунд максимум)..."

while [ $ATTEMPT -lt $MAX_ATTEMPTS ]; do
    ATTEMPT=$((ATTEMPT + 1))
    REMAINING=$((TIMEOUT - (ATTEMPT * 3)))
    
    printf "\r${CYAN}⏳ Ожидание... (%d сек осталось)   ${NC}" $REMAINING
    
    if curl -s -f -o /dev/null "$HEALTH_URL" --max-time 5 > /dev/null 2>&1; then
        READY=true
        break
    fi
    
    sleep 3
done

echo ""

if [ "$READY" = true ]; then
    print_success "Все сервисы готовы к работе!"
else
    print_error "Таймаут ожидания! Сервисы не запустились за $TIMEOUT секунд"
    echo -e "\nПроверьте логи:"
    echo "  $COMPOSE_CMD logs -f parser"
    echo "  $COMPOSE_CMD logs -f webui"
    echo -e "\nВозможные причины:"
    echo "  - Порт 8080 занят другим приложением"
    echo "  - Недостаточно ресурсов (RAM/CPU)"
    echo "  - Ошибка в конфигурации docker-compose.yml\n"
    exit 1
fi

# Открытие браузера
if [ "$NO_BROWSER" = false ]; then
    print_header "Открытие веб-интерфейса"
    
    WEB_URL="http://localhost:8082"
    print_info "Открытие $WEB_URL в браузере..."
    
    # Определение системы и открытие браузера
    if command -v xdg-open &> /dev/null; then
        # Linux
        xdg-open "$WEB_URL" > /dev/null 2>&1 &
        print_success "Браузер открыт!"
    elif command -v open &> /dev/null; then
        # macOS
        open "$WEB_URL"
        print_success "Браузер открыт!"
    else
        print_warning "Не удалось автоматически открыть браузер"
        echo -e "\nОткройте вручную: $WEB_URL\n"
    fi
fi

# Финальная информация
print_header "🎉 Готово! Проект запущен"

echo -e "
${CYAN}📊 Доступные сервисы:
   ┌─────────────────────────────────────────────┐
   │ Web UI (Blazor)        http://localhost:8082 │
   │ API                    http://localhost:8080 │
   │ Health Check           http://localhost:8080/health │
   │ Prometheus Metrics     http://localhost:9090 │
   │ Grafana Dashboards     http://localhost:3000 │
   │ Seq Logs               http://localhost:5341 │
   └─────────────────────────────────────────────┘${NC}

${CYAN}🔑 API Key (по умолчанию):
   Заголовок: X-API-Key: your-api-key-here
   Или измените в ParserWebUI/appsettings.json${NC}

${CYAN}🛠️  Управление контейнерами:
   Остановить:  $COMPOSE_CMD down
   Перезапуск: $COMPOSE_CMD restart
   Логи:        $COMPOSE_CMD logs -f
   Статус:      $COMPOSE_CMD ps${NC}

${CYAN}📚 Документация:
   README.md           Основное описание
   docs/ARCHITECTURE.md Архитектура v2.0
   docs/TESTING.md     Тестирование
   DEPLOYMENT_GUIDE.md Развёртывание${NC}

${CYAN}💡 Советы:
   - Для пересборки образов: $0 --rebuild
   - Без открытия браузера: $0 --no-browser
   - Своевременный таймаут: $0 --timeout 180${NC}
"

echo -n "Нажмите Enter для продолжения..."
read -r
