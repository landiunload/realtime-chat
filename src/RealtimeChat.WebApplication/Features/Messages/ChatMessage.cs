namespace RealtimeChat.WebApplication.Features.Messages;

/// <summary>
/// Сообщение чата. Создаётся только через фабричный метод,
/// который гарантирует корректность данных.
/// </summary>
public sealed class ChatMessage
{
    /// <summary>Максимально допустимая длина текста сообщения.</summary>
    public const int MaximumTextLength = 2000;

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

        if (text.Length > MaximumTextLength)
        {
            throw new ArgumentException(
                $"Текст сообщения не может быть длиннее {MaximumTextLength} символов.",
                nameof(text));
        }

        return new ChatMessage
        {
            Identifier = Guid.CreateVersion7(),
            RoomName = roomName.Trim(),
            SenderName = senderName.Trim(),
            Text = text.Trim(),
            SentAtUtc = DateTimeOffset.UtcNow
        };
    }
}
