namespace AsyncFileScanner.Services;

/// <summary>
/// Сервис для асинхронного чтения заголовка файла.
/// Позволяет проверить доступность файла без загрузки его целиком.
/// </summary>
public sealed class FileProbe
{
    public async Task<int> ReadHeaderAsync(
        string path,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[1024];

        // Открываем поток асинхронно (useAsync: true)
        // FileMode.Open - открываем существующий
        // FileAccess.Read - только читаем
        // FileShare.Read - разрешаем другим процессам читать этот файл пока мы его читаем
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        // Читаем в буфер. ReadAsync уважает CancellationToken.
        return await stream.ReadAsync(
            buffer.AsMemory(),
            cancellationToken);
    }
}