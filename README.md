# Лабораторная работа: Поведенческие паттерны (Observer, Strategy, Template Method)

**Выполнил:** Евсеев В.А., группа 2307А1

## Тема

Применение поведенческих паттернов проектирования (Наблюдатель, Стратегия, Шаблонный метод) при разработке системы мониторинга и оповещения о событиях.

## Цель

Получить практические навыки использования трёх поведенческих паттернов в рамках одной программы. Научиться строить гибкую архитектуру, которую легко расширять без переписывания существующего кода.

## Задание

Реализовать имитацию системы мониторинга, которая отслеживает метрики (загрузка CPU, использование памяти, сетевой трафик) и оповещает подписчиков при превышении пороговых значений.

Поддерживаемые каналы доставки:
- вывод в консоль,
- запись в файл,
- имитация отправки email.

Поддерживаемые форматы сообщений:
- обычный текст,
- JSON,
- HTML.

Архитектура должна позволять добавлять новые каналы оповещения и новые форматы сообщений без правки уже написанных классов. Все три паттерна должны работать согласованно.

## Использованные паттерны и их место в коде

### Наблюдатель

Реализован через встроенное событие C#.  
Класс `EventMonitor` содержит событие `OnMetricExceeded`. Когда значение метрики превышает порог, вызывается `OnMetricExceeded?.Invoke(...)`.  
Подписчиками выступают экземпляры классов-наследников `EventHandlerBase`, которые добавляются к событию через `+=`.

**Почему так, а не классический `Attach/Detach`:**  
Событие C# само заботится о потокобезопасном вызове и управлении списком подписчиков, что сокращает количество шаблонного кода.

### Стратегия

За форматирование сообщений отвечает интерфейс `IFormatStrategy` с методом `Format`.  
Конкретные стратегии:
- `TextFormatStrategy` – простой текст с временной меткой,
- `JsonFormatStrategy` – сериализация в JSON,
- `HtmlFormatStrategy` – оборачивание в HTML-теги.

Контекстом является базовый класс `EventHandlerBase`. Он хранит ссылку на текущую стратегию и вызывает её в методе `FormatMessage`. Стратегию можно заменить во время выполнения через метод `SetStrategy`.

### Шаблонный метод

Базовый абстрактный класс `EventHandlerBase` задаёт фиксированный порядок обработки события в методе `ProcessEvent`:
1. Форматирование сообщения (`FormatMessage`)
2. Отправка сообщения (`SendMessage`)
3. Логирование (`LogResult`)

Шаги `SendMessage` – абстрактный, его реализуют подклассы (`ConsoleHandler`, `FileHandler`, `EmailHandler`).  
Шаги `FormatMessage` и `LogResult` – виртуальные, при необходимости их можно переопределить.


## Основные классы

- `MetricData` – record с данными о метрике (имя, значение, порог, время).
- `MetricEventArgs` – аргументы события, содержат тип события и `MetricData`.
- `EventMonitor` – издатель, проверяет метрики и генерирует событие.
- `IFormatStrategy` – интерфейс стратегии форматирования.
- `TextFormatStrategy`, `JsonFormatStrategy`, `HtmlFormatStrategy` – конкретные стратегии.
- `EventHandlerBase` – абстрактный базовый класс с шаблонным методом.
- `ConsoleHandler`, `FileHandler`, `EmailHandler` – конкретные подписчики.

## Пример работы

При запуске программа имитирует несколько проверок:
 Проверка CPU
[Monitor] Проверка CPU_Load: 85,5 vs 80
[Console] [2026-04-14 12:34:56] Событие: CPU_Load_Exceeded, CPU_Load: 85,5 (порог: 80)
[Console] {"timestamp":"...","message":"..."}
[Email] Отправлено письмо с телом: {...}
[Console Log] Обработана метрика CPU_Load в 14.04.2026 12:34:56

Помимо консольного вывода, создаются файлы:
- `notifications.log` – уведомления от `FileHandler` (в HTML),
- `handler.log` – служебный лог обработчика.

## Расширяемость

Чтобы добавить новый формат (например, XML):
- создать класс `XmlFormatStrategy : IFormatStrategy`,
- передать его экземпляр в конструктор нужного обработчика.

Чтобы добавить новый канал (например, SMS):
- создать класс `SmsHandler : EventHandlerBase`,
- реализовать `SendMessage`,
- подписать экземпляр на событие монитора.

Никакие существующие классы при этом не меняются.

## Исходный код

Полный код находится в файле `Program.cs` этого репозитория.  
Ключевые фрагменты приведены ниже.

```csharp

// Данные метрики
public record MetricData(string MetricName, double Value, double Threshold, DateTime Timestamp);

// Аргументы события
public class MetricEventArgs : EventArgs
{
    public string EventType { get; }
    public MetricData Data { get; }
    // ...
}

// Издатель (Observer)
public class EventMonitor
{
    public event EventHandler<MetricEventArgs>? OnMetricExceeded;
    public void CheckMetric(string metricName, double value, double threshold)
    {
        if (value > threshold)
        {
            var data = new MetricData(metricName, value, threshold, DateTime.Now);
            var args = new MetricEventArgs($"{metricName}_Exceeded", data);
            OnMetricExceeded?.Invoke(this, args);
        }
    }
}

// Стратегия форматирования
public interface IFormatStrategy
{
    string Format(string message, DateTime timestamp);
}

// Базовый обработчик (Template Method)
public abstract class EventHandlerBase
{
    protected IFormatStrategy _formatStrategy;
    public void ProcessEvent(object sender, MetricEventArgs e)
    {
        string message = FormatMessage(e.EventType, e.Data);
        SendMessage(message);
        LogResult(e.Data);
    }
    protected virtual string FormatMessage(string eventType, MetricData data)
    {
        string content = $"Событие: {eventType}, {data}";
        return _formatStrategy.Format(content, DateTime.Now);
    }
    protected abstract void SendMessage(string formattedMessage);
    protected virtual void LogResult(MetricData data) { }
    public void SetStrategy(IFormatStrategy strategy) => _formatStrategy = strategy;
}

// Один из подписчиков
public class ConsoleHandler : EventHandlerBase
{
    public ConsoleHandler(IFormatStrategy strategy) : base(strategy) { }
    protected override void SendMessage(string formattedMessage)
        => Console.WriteLine($"[Console] {formattedMessage}");
    protected override void LogResult(MetricData data)
        => Console.WriteLine($"[Console Log] Обработана метрика {data.MetricName} в {data.Timestamp}");
}
