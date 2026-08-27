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
        var normalizedRoomName = NormalizeRoomName(roomName);
        var normalizedSenderName = NormalizeSenderName(senderName);
        var normalizedText = NormalizeText(text);

        return new ChatMessage
        {
            Identifier = Guid.CreateVersion7(),
            RoomName = normalizedRoomName,
            SenderName = normalizedSenderName,
            Text = normalizedText,
            SentAtUtc = DateTimeOffset.UtcNow
        };
    }

    internal static string NormalizeRoomName(string roomName) => NormalizeRequiredValue(
        roomName,
        MaximumNameLength,
        nameof(roomName),
        "Название комнаты");

    internal static string NormalizeSenderName(string senderName) => NormalizeRequiredValue(
        senderName,
        MaximumNameLength,
        nameof(senderName),
        "Имя отправителя");

    private static string NormalizeText(string text) => NormalizeRequiredValue(
        text,
        MaximumTextLength,
        nameof(text),
        "Текст сообщения");

    private static string NormalizeRequiredValue(
        string value,
        int maximumLength,
        string parameterName,
        string subjectDescription)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

        var normalizedValue = value.Trim();
        if (normalizedValue.Length > maximumLength)
        {
            throw new ArgumentException(
                $"{subjectDescription} не может быть длиннее {maximumLength} символов.",
                parameterName);
        }

        return normalizedValue;
    }
}
