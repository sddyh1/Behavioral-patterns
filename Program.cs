using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace MonitoringSystem
{
    public record MetricData(string MetricName, double Value, double Threshold, DateTime Timestamp)
    {
        public override string ToString() => $"{MetricName}: {Value} (порог: {Threshold})";
    }

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

    //Наблюдатель
    public class EventMonitor
    {
        public event EventHandler<MetricEventArgs>? OnMetricExceeded;

        public void CheckMetric(string metricName, double value, double threshold)
        {
            Console.WriteLine($"[Monitor] Проверка {metricName}: {value} vs {threshold}");
            if (value > threshold)
            {
                var data = new MetricData(metricName, value, threshold, DateTime.Now);
                var args = new MetricEventArgs($"{metricName}_Exceeded", data);
                OnMetricExceeded?.Invoke(this, args);
            }
        }
    }

    //Стратегия
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

    // "Шаблонный метод
    public abstract class EventHandlerBase
    {
        protected IFormatStrategy _formatStrategy;

        protected EventHandlerBase(IFormatStrategy strategy)
        {
            _formatStrategy = strategy;
        }

        public void SetStrategy(IFormatStrategy strategy)
        {
            _formatStrategy = strategy;
        }

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

        protected virtual void LogResult(MetricData data)
        {
        }
    }

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
            File.AppendAllText("handler.log", $"{DateTime.Now}: {data.MetricName} = {data.Value}{Environment.NewLine}");
        }
    }

    public class EmailHandler : EventHandlerBase
    {
        public EmailHandler(IFormatStrategy strategy) : base(strategy) { }

        protected override void SendMessage(string formattedMessage)
        {
            Console.WriteLine($"[Email] Отправлено письмо с телом: {formattedMessage}");
        }
    }

    class Program
    {
        static void Main()
        {
            Console.WriteLine("Система мониторинга и оповещения\n");

            var monitor = new EventMonitor();

            var consoleText = new ConsoleHandler(new TextFormatStrategy());
            var consoleJson = new ConsoleHandler(new JsonFormatStrategy());
            var fileHtml = new FileHandler(new HtmlFormatStrategy());
            var emailJson = new EmailHandler(new JsonFormatStrategy());

            monitor.OnMetricExceeded += consoleText.ProcessEvent;
            monitor.OnMetricExceeded += consoleJson.ProcessEvent;
            monitor.OnMetricExceeded += fileHtml.ProcessEvent;
            monitor.OnMetricExceeded += emailJson.ProcessEvent;

            Console.WriteLine("Проверка CPU");
            monitor.CheckMetric("CPU_Load", 85.5, 80.0);

            Console.WriteLine("\nПроверка Memory");
            monitor.CheckMetric("Memory_Usage", 2048.0, 1500.0);

            Console.WriteLine("\nПроверка Network");
            monitor.CheckMetric("Network_Traffic", 120.0, 100.0);

            Console.WriteLine("\nСмена стратегии для consoleText на HTML");
            consoleText.SetStrategy(new HtmlFormatStrategy());
            monitor.CheckMetric("CPU_Load", 90.0, 80.0); 


        }
    }
}
