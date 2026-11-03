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

// Парсим расширения, приводим к нижнему регистру и добавляем точку, если её нет
var extensions = extInput.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                         .Select(e => e.StartsWith('.') ? e.ToLowerInvariant() : $".{e.ToLowerInvariant()}")
                         .ToHashSet();

var options = new ScanOptions(pathInput, extensions);

// Создаем сервисы
var probe = new FileProbe();
var scanner = new FileScanner(probe);

// 3. Настройка отмены
using var cts = new CancellationTokenSource();

// Запускаем сканирование в фоне (Task), чтобы основной поток мог слушать ввод пользователя
Console.WriteLine();
Console.WriteLine("Сканирование...");
Console.WriteLine("Нажмите Enter для отмены.");

var scanTask = scanner.ScanAsync(options, cts.Token);

// Ждем либо завершения сканирования, либо нажатия Enter
var inputTask = Console.In.ReadLineAsync();

// Task.WhenAny завершится, когда выполнится первая из задач
var completedTask = await Task.WhenAny(scanTask, inputTask);

if (completedTask == inputTask)
{
    // Пользователь нажал Enter -> отменяем токен
    cts.Cancel();
    Console.WriteLine("Сканирование отменено пользователем.");

    try
    {
        // Дожидаемся завершения задачи, чтобы поймать исключение отмены корректно, 
        // или просто игнорируем, если нам важна только статистика до момента отмены.
        // Но так как мы вышли из цикла по break, задача должна завершиться нормально.
        var partialResult = await scanTask;

        Console.WriteLine($"Обработано файлов: {partialResult.TotalFilesScanned}");
        Console.WriteLine($"Успешно найдено: {partialResult.SuccessfulFiles}");
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Операция прервана.");
    }
}
else
{
    // Сканирование завершилось само
    try
    {
        var summary = await scanTask;

        Console.WriteLine();
        Console.WriteLine("Найденные файлы:");
        Console.WriteLine();

        for (int i = 0; i < summary.Results.Count; i++)
        {
            var file = summary.Results[i];
            Console.WriteLine($"[{i + 1}] {file.FileName,-20} {file.SizeInBytes} байт");
        }

        Console.WriteLine();
        Console.WriteLine("Сканирование завершено.");
        Console.WriteLine($"Всего найдено файлов: {summary.SuccessfulFiles}");
        Console.WriteLine($"Общий размер: {summary.TotalSizeBytes} байт");

        if (summary.Errors.Any())
        {
            Console.WriteLine();
            Console.WriteLine($"Ошибок доступа: {summary.Errors.Count}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Критическая ошибка: {ex.Message}");
    }
}