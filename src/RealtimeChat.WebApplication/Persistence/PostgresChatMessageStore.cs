using Microsoft.EntityFrameworkCore;
using RealtimeChat.WebApplication.Features.Messages;

namespace RealtimeChat.WebApplication.Persistence;

/// <summary>Хранилище сообщений поверх PostgreSQL.</summary>
public sealed class PostgresChatMessageStore(ChatDatabaseContext databaseContext) : IChatMessageStore
{
    /// <inheritdoc />
    public async Task SaveMessageAsync(ChatMessage chatMessage, CancellationToken cancellationToken)
    {
        databaseContext.ChatMessages.Add(chatMessage);
        await databaseContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChatMessage>> FindRecentMessagesAsync(
        string roomName,
        int messagesCount,
        CancellationToken cancellationToken)
    {
        // Берём последние N сообщений и разворачиваем их в хронологический порядок.
        // Тай-брейк по идентификатору: два сообщения могут попасть на одну метку
        // времени, и без него их взаимный порядок не определён — история «прыгала» бы
        // между запросами. Guid v7 монотонен во времени, поэтому порядок совпадает
        // с фактическим порядком отправки.
        var recentMessagesInReverseOrder = await databaseContext.ChatMessages
            .Where(chatMessage => chatMessage.RoomName == roomName)
            .OrderByDescending(chatMessage => chatMessage.SentAtUtc)
            .ThenByDescending(chatMessage => chatMessage.Identifier)
            .Take(messagesCount)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        recentMessagesInReverseOrder.Reverse();
        return recentMessagesInReverseOrder;
    }
}
