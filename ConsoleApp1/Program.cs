using AsyncFileScanner.Models;
using AsyncFileScanner.Services;

Console.WriteLine("=== FILE SCANNER ===");
Console.WriteLine();

// 1. Ввод пути
Console.Write("Введите путь к директории: ");
string? pathInput = Console.ReadLine();
if (string.IsNullOrWhiteSpace(pathInput))
{
    Console.WriteLine("Путь не указан.");
    return;
}

// 2. Ввод расширений
Console.Write("Введите расширения через запятую (например .cs,.txt): ");
string? extInput = Console.ReadLine();
if (string.IsNullOrWhiteSpace(extInput))
{
    Console.WriteLine("Расширения не указаны.");
    return;
}

var extensions = extInput.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                         .Select(e => e.StartsWith('.') ? e.ToLowerInvariant() : $".{e.ToLowerInvariant()}")
                         .ToHashSet();

// 3. Ввод фильтра по размеру
long minBytes = 0;
long maxBytes = long.MaxValue;

Console.Write("Минимальный размер (КБ, 0 = без ограничения): ");
if (long.TryParse(Console.ReadLine(), out long minKb) && minKb > 0)
{
    minBytes = minKb * 1024;
}

Console.Write("Максимальный размер (КБ, 0 = без ограничения): ");
if (long.TryParse(Console.ReadLine(), out long maxKb) && maxKb > 0)
{
    maxBytes = maxKb * 1024;
}

// 4. Ввод ограничения параллелизма
Console.Write("Максимальное количество одновременных операций: ");
int maxConcurrency = 4;
if (int.TryParse(Console.ReadLine(), out int parsed) && parsed > 0)
{
    maxConcurrency = parsed;
}

var options = new ScanOptions(
    pathInput,
    extensions,
    minBytes,
    maxBytes,
    maxConcurrency);

var probe = new FileProbe();
var scanner = new FileScanner(probe);

using var cts = new CancellationTokenSource();

Console.WriteLine();
Console.WriteLine("Сканирование...");
Console.WriteLine("Нажмите Enter для отмены.");

var scanTask = scanner.ScanAsync(options, cts.Token);
var inputTask = Console.In.ReadLineAsync();

var completedTask = await Task.WhenAny(scanTask, inputTask);

if (completedTask == inputTask)
{
    cts.Cancel();
    Console.WriteLine("Сканирование отменено пользователем.");

    try
    {
        var partialResult = await scanTask;
        PrintSummary(partialResult, maxConcurrency);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Операция прервана.");
    }
}
else
{
    try
    {
        var summary = await scanTask;
        PrintSummary(summary, maxConcurrency);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Критическая ошибка: {ex.Message}");
    }
}

// ====================== Вспомогательные методы ======================

static void PrintSummary(ScanSummary summary, int maxConcurrency)
{
    Console.WriteLine();
    Console.WriteLine("=== РЕЗУЛЬТАТ СКАНИРОВАНИЯ ===");
    Console.WriteLine();

    Console.WriteLine($"Просмотрено файлов: {summary.TotalFilesScanned}");
    Console.WriteLine($"Подходящих файлов:  {summary.SuccessfulFiles}");
    Console.WriteLine($"Общий размер:       {FormatSize(summary.TotalSizeBytes)}");
    Console.WriteLine($"Ошибок:             {summary.Errors.Count}");
    Console.WriteLine($"Время выполнения:   {summary.ElapsedTime.TotalSeconds:F2} сек.");
    Console.WriteLine();
    Console.WriteLine($"Ограничение: {maxConcurrency}");
    Console.WriteLine($"Максимум одновременно обрабатываемых файлов: {summary.MaxObservedConcurrency}");

    if (summary.Results.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine("Найденные файлы:");
        for (int i = 0; i < summary.Results.Count; i++)
        {
            var file = summary.Results[i];
            Console.WriteLine($"[{i + 1}] {file.FileName,-30} {FormatSize(file.SizeInBytes)}");
        }
    }
}

static string FormatSize(long bytes)
{
    const double KB = 1024.0;
    const double MB = KB * 1024;
    const double GB = MB * 1024;

    if (bytes < KB) return $"{bytes} Б";
    if (bytes < MB) return $"{bytes / KB:F1} КБ";
    if (bytes < GB) return $"{bytes / MB:F1} МБ";
    return $"{bytes / GB:F2} ГБ";
}