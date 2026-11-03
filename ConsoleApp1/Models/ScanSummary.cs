using System.Diagnostics;

namespace AsyncFileScanner.Models;

public sealed record ScanSummary(
    int TotalFilesScanned,
    int SuccessfulFiles,
    long TotalSizeBytes,
    TimeSpan ElapsedTime,
    List<ScanResult> Results,
    List<string> Errors,
    int MaxObservedConcurrency
);