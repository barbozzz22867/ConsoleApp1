using System.Diagnostics; // Нужно для типа TimeSpan

namespace AsyncFileScanner.Models;

// Здесь хранятся общие результаты сканирования.
public sealed record ScanSummary(
    int TotalFilesScanned,
    int SuccessfulFiles,
    long TotalSizeBytes,
    TimeSpan ElapsedTime,
    List<ScanResult> Results,
    List<string> Errors
);