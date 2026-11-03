using System.Diagnostics;
using AsyncFileScanner.Models;

namespace AsyncFileScanner.Services;

/// <summary>
/// Основной сервис сканирования директории.
/// </summary>
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
        var results = new List<ScanResult>();
        var errors = new List<string>();

        int scannedFiles = 0;
        long totalBytes = 0;

        // Получаем все файлы рекурсивно
        var files = Directory.EnumerateFiles(
            options.DirectoryPath,
            "*.*",
            SearchOption.AllDirectories);

        foreach (var filePath in files)
        {
            // 1. Проверяем отмену перед каждой итерацией
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            scannedFiles++;

            // 2. Фильтрация по расширениям
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            // Если расширение не входит в список разрешенных, пропускаем файл
            if (!options.Extensions.Contains(extension))
            {
                continue;
            }

            try
            {
                // 3. Получаем информацию о файле для размера
                var fileInfo = new FileInfo(filePath);

                // 4. Асинхронно "прощупываем" файл (читаем заголовок)
                // Это нужно, чтобы убедиться, что файл доступен для чтения
                await _probe.ReadHeaderAsync(filePath, cancellationToken);

                // Если добрались сюда, файл доступен
                var result = new ScanResult(
                    FileName: Path.GetFileName(filePath),
                    SizeInBytes: fileInfo.Length
                );

                results.Add(result);
                totalBytes += fileInfo.Length;
            }
            catch (OperationCanceledException)
            {
                // Если отмена произошла внутри ReadHeaderAsync, пробрасываем её выше
                throw;
            }
            catch (Exception ex)
            {
                // Ловим ошибки доступа (UnauthorizedAccessException) или IO ошибки
                errors.Add($"Ошибка чтения '{filePath}': {ex.Message}");
            }
        }

        stopwatch.Stop();

        return new ScanSummary(
            scannedFiles,
            results.Count,
            totalBytes,
            stopwatch.Elapsed,
            results,
            errors);
    }

    private static void ValidateOptions(ScanOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.DirectoryPath))
            throw new ArgumentException("Путь к директории не может быть пустым.", nameof(options.DirectoryPath));

        if (!Directory.Exists(options.DirectoryPath))
            throw new DirectoryNotFoundException($"Директория '{options.DirectoryPath}' не найдена.");

        if (options.Extensions == null || options.Extensions.Count == 0)
            throw new ArgumentException("Список расширений не может быть пустым.", nameof(options.Extensions));
    }
}