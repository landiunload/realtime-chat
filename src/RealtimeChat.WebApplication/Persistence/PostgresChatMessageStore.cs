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
        // Берём последние N сообщений и разворачиваем их в хронологический порядок
        var recentMessagesInReverseOrder = await databaseContext.ChatMessages
            .Where(chatMessage => chatMessage.RoomName == roomName)
            .OrderByDescending(chatMessage => chatMessage.SentAtUtc)
            .Take(messagesCount)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        recentMessagesInReverseOrder.Reverse();
        return recentMessagesInReverseOrder;
    }
}
