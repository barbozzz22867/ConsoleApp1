namespace AsyncFileScanner.Models;

public sealed record ScanOptions(
    string DirectoryPath,
    HashSet<string> Extensions,
    long MinSizeBytes,      // Минимальный размер в байтах
    long MaxSizeBytes,      // Максимальный размер в байтах
    int MaxConcurrency      // Максимум одновременных операций
);