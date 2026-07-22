namespace RealtimeChat.WebApplication.Features.Messages;

/// <summary>
/// Сообщение чата. Создаётся только через фабричный метод,
/// который гарантирует корректность данных.
/// </summary>
public sealed class ChatMessage
{
    /// <summary>Максимально допустимая длина текста сообщения.</summary>
    public const int MaximumTextLength = 2000;

    /// <summary>
    /// Максимально допустимая длина названия комнаты и имени отправителя.
    /// Значение совпадает с шириной колонок в <c>ChatDatabaseContext</c>: проверка
    /// здесь и ограничение схемы обязаны сходиться, иначе слишком длинное имя
    /// проходит фабрику и падает уже на вставке в базу.
    /// </summary>
    public const int MaximumNameLength = 64;

    /// <summary>Уникальный идентификатор сообщения.</summary>
    public Guid Identifier { get; private set; }

    /// <summary>Название комнаты, в которую отправлено сообщение.</summary>
    public string RoomName { get; private set; } = string.Empty;

    /// <summary>Имя отправителя.</summary>
    public string SenderName { get; private set; } = string.Empty;

    /// <summary>Текст сообщения.</summary>
    public string Text { get; private set; } = string.Empty;

    /// <summary>Момент отправки в формате UTC.</summary>
    public DateTimeOffset SentAtUtc { get; private set; }

    // Приватный конструктор требуется Entity Framework Core
    private ChatMessage() { }

    /// <summary>Создаёт сообщение с проверкой корректности входных данных.</summary>
    public static ChatMessage Create(string roomName, string senderName, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomName);
        ArgumentException.ThrowIfNullOrWhiteSpace(senderName);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        // Пределы проверяются по обрезанным значениям: в базу уезжают именно они,
        // поэтому окаймляющие пробелы не должны решать судьбу сообщения.
        var trimmedRoomName = roomName.Trim();
        var trimmedSenderName = senderName.Trim();
        var trimmedText = text.Trim();

        EnsureLengthWithinLimit(trimmedRoomName, MaximumNameLength, nameof(roomName), "Название комнаты");
        EnsureLengthWithinLimit(trimmedSenderName, MaximumNameLength, nameof(senderName), "Имя отправителя");
        EnsureLengthWithinLimit(trimmedText, MaximumTextLength, nameof(text), "Текст сообщения");

        return new ChatMessage
        {
            Identifier = Guid.CreateVersion7(),
            RoomName = trimmedRoomName,
            SenderName = trimmedSenderName,
            Text = trimmedText,
            SentAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static void EnsureLengthWithinLimit(
        string value,
        int maximumLength,
        string parameterName,
        string subjectDescription)
    {
        if (value.Length > maximumLength)
        {
            throw new ArgumentException(
                $"{subjectDescription} не может быть длиннее {maximumLength} символов.",
                parameterName);
        }
    }
}
