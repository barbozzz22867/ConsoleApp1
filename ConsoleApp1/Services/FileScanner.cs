using System.Diagnostics;
using AsyncFileScanner.Models;

namespace AsyncFileScanner.Services;

public sealed class FileScanner
{
    private readonly FileProbe _probe;

    public FileScanner(FileProbe probe)
    {
        _probe = probe;
    }

    public async Task<ScanSummary> ScanAsync(
        ScanOptions options,
        CancellationToken cancellationToken)
    {
        ValidateOptions(options);

        var stopwatch = Stopwatch.StartNew();

        // Материализуем список файлов.
        // Важно: EnumerateFiles ленивый, но для Task.WhenAll нам нужен готовый список.
        var files = Directory.EnumerateFiles(
            options.DirectoryPath,
            "*.*",
            SearchOption.AllDirectories).ToList();

        // Семафор ограничивает количество одновременных операций
        using var semaphore = new SemaphoreSlim(options.MaxConcurrency);

        // Счётчики для отслеживания реальной параллельности
        int activeOperations = 0;
        int maxObservedOperations = 0;

        // Создаём задачи для каждого файла
        var tasks = files.Select(async path =>
        {
            // Ждём, пока семафор разрешит войти
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                // Увеличиваем счётчик активных операций
                int current = Interlocked.Increment(ref activeOperations);

                // Обновляем максимум, если текущее значение больше
                UpdateMax(ref maxObservedOperations, current);

                try
                {
                    return await ProcessFileAsync(path, options, cancellationToken);
                }
                finally
                {
                    // Уменьшаем счётчик после обработки
                    Interlocked.Decrement(ref activeOperations);
                }
            }
            finally
            {
                // Освобождаем семафор в любом случае
                semaphore.Release();
            }
        });

        // Ждём завершения всех задач
        var allResults = await Task.WhenAll(tasks);

        stopwatch.Stop();

        // Собираем итоговую статистику
        var results = new List<ScanResult>();
        var errors = new List<string>();
        long totalBytes = 0;

        foreach (var (result, error) in allResults)
        {
            if (error != null)
            {
                errors.Add(error);
            }
            else if (result != null)
            {
                results.Add(result);
                totalBytes += result.SizeInBytes;
            }
        }

        return new ScanSummary(
            files.Count,
            results.Count,
            totalBytes,
            stopwatch.Elapsed,
            results,
            errors,
            maxObservedOperations);
    }

    /// <summary>
    /// Обработка одного файла: проверка размера и чтение заголовка.
    /// Возвращает кортеж: (результат, ошибка).
    /// </summary>
    private async Task<(ScanResult? Result, string? Error)> ProcessFileAsync(
        string path,
        ScanOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            // Фильтр по расширению
            var extension = Path.GetExtension(path).ToLowerInvariant();
            if (!options.Extensions.Contains(extension))
                return (null, null); // не подходит, но это не ошибка

            var fileInfo = new FileInfo(path);
            long size = fileInfo.Length;

            // Фильтр по размеру
            if (size < options.MinSizeBytes || size > options.MaxSizeBytes)
                return (null, null); // не подходит по размеру

            // Асинхронно читаем заголовок, чтобы проверить доступность
            await _probe.ReadHeaderAsync(path, cancellationToken);

            var result = new ScanResult(
                FileName: Path.GetFileName(path),
                SizeInBytes: size
            );

            return (result, null);
        }
        catch (OperationCanceledException)
        {
            throw; // Отмену пробрасываем дальше
        }
        catch (Exception ex)
        {
            return (null, $"Ошибка чтения '{path}': {ex.Message}");
        }
    }

    /// <summary>
    /// Потокобезопасное обновление максимума через CompareExchange.
    /// </summary>
    private static void UpdateMax(ref int maxValue, int newValue)
    {
        while (true)
        {
            int observed = Volatile.Read(ref maxValue);
            if (newValue <= observed) return;

            // Пытаемся записать newValue, если maxValue всё ещё равен observed
            if (Interlocked.CompareExchange(ref maxValue, newValue, observed) == observed)
                return;
            // Иначе кто-то успел изменить maxValue раньше — повторяем попытку
        }
    }

    private static void ValidateOptions(ScanOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.DirectoryPath))
            throw new ArgumentException("Путь к директории не может быть пустым.");

        if (!Directory.Exists(options.DirectoryPath))
            throw new DirectoryNotFoundException($"Директория '{options.DirectoryPath}' не найдена.");

        if (options.Extensions == null || options.Extensions.Count == 0)
            throw new ArgumentException("Список расширений не может быть пустым.");

        if (options.MinSizeBytes < 0)
            throw new ArgumentException("Минимальный размер не может быть отрицательным.");

        if (options.MaxSizeBytes < options.MinSizeBytes)
            throw new ArgumentException("Максимальный размер должен быть больше или равен минимальному.");

        if (options.MaxConcurrency <= 0)
            throw new ArgumentException("Количество одновременных операций должно быть больше 0.");
    }
}