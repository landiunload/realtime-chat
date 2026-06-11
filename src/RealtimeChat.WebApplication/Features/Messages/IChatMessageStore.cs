namespace RealtimeChat.WebApplication.Features.Messages;

/// <summary>
/// Контракт хранилища сообщений.
/// Хаб зависит от интерфейса, а не от конкретной базы данных —
/// в тестах хранилище подменяется без поднятия PostgreSQL.
/// </summary>
public interface IChatMessageStore
{
    /// <summary>Сохраняет сообщение.</summary>
    Task SaveMessageAsync(ChatMessage chatMessage, CancellationToken cancellationToken);

    /// <summary>Возвращает последние сообщения комнаты в хронологическом порядке.</summary>
    Task<IReadOnlyList<ChatMessage>> FindRecentMessagesAsync(string roomName, int messagesCount, CancellationToken cancellationToken);
}
