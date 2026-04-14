using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace MonitoringSystem
{
    // ========================== Данные метрики ==========================
    public record MetricData(string MetricName, double Value, double Threshold, DateTime Timestamp)
    {
        public override string ToString() => $"{MetricName}: {Value} (порог: {Threshold})";
    }

    // ========================== Аргументы события ==========================
    public class MetricEventArgs : EventArgs
    {
        public string EventType { get; }
        public MetricData Data { get; }

        public MetricEventArgs(string eventType, MetricData data)
        {
            EventType = eventType ?? throw new ArgumentNullException(nameof(eventType));
            Data = data ?? throw new ArgumentNullException(nameof(data));
        }
    }

    // ========================== Паттерн "Наблюдатель" (издатель) ==========================
    public class EventMonitor
    {
        // Событие – реализация Observer в C#
        public event EventHandler<MetricEventArgs>? OnMetricExceeded;

        public void CheckMetric(string metricName, double value, double threshold)
        {
            Console.WriteLine($"[Monitor] Проверка {metricName}: {value} vs {threshold}");
            if (value > threshold)
            {
                var data = new MetricData(metricName, value, threshold, DateTime.Now);
                var args = new MetricEventArgs($"{metricName}_Exceeded", data);
                OnMetricExceeded?.Invoke(this, args);   // уведомление подписчиков
            }
        }
    }

    // ========================== Паттерн "Стратегия" (форматирование) ==========================
    public interface IFormatStrategy
    {
        string Format(string message, DateTime timestamp);
    }

    public class TextFormatStrategy : IFormatStrategy
    {
        public string Format(string message, DateTime timestamp) =>
            $"[{timestamp:yyyy-MM-dd HH:mm:ss}] {message}";
    }

    public class JsonFormatStrategy : IFormatStrategy
    {
        public string Format(string message, DateTime timestamp)
        {
            var obj = new { timestamp, message };
            return JsonSerializer.Serialize(obj);
        }
    }

    public class HtmlFormatStrategy : IFormatStrategy
    {
        public string Format(string message, DateTime timestamp) =>
            $"<div><strong>{timestamp:yyyy-MM-dd HH:mm:ss}</strong> <p>{message}</p></div>";
    }

    // ========================== Паттерн "Шаблонный метод" ==========================
    public abstract class EventHandlerBase
    {
        protected IFormatStrategy _formatStrategy;

        protected EventHandlerBase(IFormatStrategy strategy)
        {
            _formatStrategy = strategy;
        }

        // Смена стратегии во время выполнения
        public void SetStrategy(IFormatStrategy strategy)
        {
            _formatStrategy = strategy;
        }

        // Шаблонный метод: определяет скелет алгоритма обработки события
        public void ProcessEvent(object sender, MetricEventArgs e)
        {
            // 1. Форматирование сообщения с использованием стратегии
            string message = FormatMessage(e.EventType, e.Data);
            // 2. Отправка уведомления (абстрактный шаг)
            SendMessage(message);
            // 3. Логирование (hook-метод, можно переопределить)
            LogResult(e.Data);
        }

        // Форматирование – использует стратегию, но может быть переопределено
        protected virtual string FormatMessage(string eventType, MetricData data)
        {
            string content = $"Событие: {eventType}, {data}";
            return _formatStrategy.Format(content, DateTime.Now);
        }

        // Абстрактные шаги, реализуемые в конкретных обработчиках
        protected abstract void SendMessage(string formattedMessage);

        // Hook-метод – необязательная операция
        protected virtual void LogResult(MetricData data)
        {
            // базовое логирование (можно оставить пустым)
        }
    }

    // ===== Конкретные обработчики (подписчики) =====
    public class ConsoleHandler : EventHandlerBase
    {
        public ConsoleHandler(IFormatStrategy strategy) : base(strategy) { }

        protected override void SendMessage(string formattedMessage)
        {
            Console.WriteLine($"[Console] {formattedMessage}");
        }

        protected override void LogResult(MetricData data)
        {
            Console.WriteLine($"[Console Log] Обработана метрика {data.MetricName} в {data.Timestamp}");
        }
    }

    public class FileHandler : EventHandlerBase
    {
        private readonly string _filePath = "notifications.log";

        public FileHandler(IFormatStrategy strategy) : base(strategy) { }

        protected override void SendMessage(string formattedMessage)
        {
            File.AppendAllText(_filePath, formattedMessage + Environment.NewLine);
        }

        protected override void LogResult(MetricData data)
        {
            // Дополнительное логирование в отдельный файл
            File.AppendAllText("handler.log", $"{DateTime.Now}: {data.MetricName} = {data.Value}{Environment.NewLine}");
        }
    }

    // Имитация email-обработчика (дополнительный тип подписчика)
    public class EmailHandler : EventHandlerBase
    {
        public EmailHandler(IFormatStrategy strategy) : base(strategy) { }

        protected override void SendMessage(string formattedMessage)
        {
            // Имитация отправки email
            Console.WriteLine($"[Email] Отправлено письмо с телом: {formattedMessage}");
        }
    }

    // ========================== Демонстрация ==========================
    class Program
    {
        static void Main()
        {
            Console.WriteLine("=== Система мониторинга и оповещения ===\n");

            // 1. Создаём издателя (Subject)
            var monitor = new EventMonitor();

            // 2. Создаём подписчиков с разными стратегиями форматирования
            var consoleText = new ConsoleHandler(new TextFormatStrategy());
            var consoleJson = new ConsoleHandler(new JsonFormatStrategy());
            var fileHtml = new FileHandler(new HtmlFormatStrategy());
            var emailJson = new EmailHandler(new JsonFormatStrategy());

            // 3. Подписка на событие
            monitor.OnMetricExceeded += consoleText.ProcessEvent;
            monitor.OnMetricExceeded += consoleJson.ProcessEvent;
            monitor.OnMetricExceeded += fileHtml.ProcessEvent;
            monitor.OnMetricExceeded += emailJson.ProcessEvent;

            // 4. Имитация проверки метрик
            Console.WriteLine("--- Проверка CPU ---");
            monitor.CheckMetric("CPU_Load", 85.5, 80.0);

            Console.WriteLine("\n--- Проверка Memory ---");
            monitor.CheckMetric("Memory_Usage", 2048.0, 1500.0);

            Console.WriteLine("\n--- Проверка Network ---");
            monitor.CheckMetric("Network_Traffic", 120.0, 100.0);

            // 5. Демонстрация смены стратегии во время выполнения
            Console.WriteLine("\n--- Смена стратегии для consoleText на HTML ---");
            consoleText.SetStrategy(new HtmlFormatStrategy());
            monitor.CheckMetric("CPU_Load", 90.0, 80.0);   // повторное событие

            Console.WriteLine("\nНажмите любую клавишу для выхода...");
            Console.ReadKey();
        }
    }
}