namespace AsyncFileScanner.Models;

// Модель результата обработки одного файла.
public sealed record ScanResult(
    string FileName,
    long SizeInBytes,
    string? ErrorMessage = null
);