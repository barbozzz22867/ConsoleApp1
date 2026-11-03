namespace AsyncFileScanner.Models;

// Модель содержит настройки сканирования.
public sealed record ScanOptions(
    string DirectoryPath,
    HashSet<string> Extensions
);