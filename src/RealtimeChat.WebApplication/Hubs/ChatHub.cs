using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR;
using RealtimeChat.WebApplication.Features.Messages;

namespace RealtimeChat.WebApplication.Hubs;

/// <summary>
/// Сообщение в том виде, в каком оно уходит клиенту.
/// Имена полей в JSON закреплены явно, чтобы клиент не зависел
/// от политики именования сериализатора.
/// </summary>
public sealed record ChatMessageSnapshot(
    [property: JsonPropertyName("identifier")] Guid Identifier,
    [property: JsonPropertyName("senderName")] string SenderName,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("sentAtUtc")] DateTimeOffset SentAtUtc);

/// <summary>Контракт клиентских методов: что сервер может вызвать у подключённого клиента.</summary>
public interface IChatClient
{
    /// <summary>Доставляет клиенту новое сообщение.</summary>
    Task ReceiveMessage(Guid messageIdentifier, string senderName, string text, DateTimeOffset sentAtUtc);

    /// <summary>Доставляет клиенту историю комнаты одним пакетом, в хронологическом порядке.</summary>
    Task ReceiveMessageHistory(IReadOnlyList<ChatMessageSnapshot> recentMessages);

    /// <summary>Сообщает клиенту о входе участника в комнату.</summary>
    Task ParticipantJoined(string participantName);
}

/// <summary>
/// Хаб чата реального времени.
/// Строго типизирован (Hub&lt;IChatClient&gt;) — опечатка в имени клиентского метода
/// становится ошибкой компиляции, а не молчаливым сбоем в продакшене.
/// </summary>
public sealed partial class ChatHub(
    IChatMessageStore chatMessageStore,
    ILogger<ChatHub> logger) : Hub<IChatClient>
{
    private static readonly object ParticipantSessionItemKey = new();
    private const int RecentMessagesCountToSendOnJoin = 50;

    private sealed record ParticipantSession(string RoomName, string ParticipantName);

    /// <summary>Подключает участника к комнате и отправляет ему последние сообщения.</summary>
    public async Task JoinRoom(string roomName, string participantName)
    {
        var normalizedRoomName = ChatMessage.NormalizeRoomName(roomName);
        var normalizedParticipantName = ChatMessage.NormalizeSenderName(participantName);

        if (TryGetParticipantSession() is { } previousSession)
        {
            Context.Items.Remove(ParticipantSessionItemKey);
            await Groups.RemoveFromGroupAsync(
                Context.ConnectionId,
                previousSession.RoomName,
                Context.ConnectionAborted);
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            normalizedRoomName,
            Context.ConnectionAborted);
        Context.Items[ParticipantSessionItemKey] = new ParticipantSession(
            normalizedRoomName,
            normalizedParticipantName);

        LogParticipantJoined(
            logger,
            normalizedParticipantName,
            normalizedRoomName,
            Context.ConnectionId);

        // Новому участнику — история, остальным — уведомление о входе
        var recentMessages = await chatMessageStore.FindRecentMessagesAsync(
            normalizedRoomName,
            RecentMessagesCountToSendOnJoin,
            Context.ConnectionAborted);

        // История уходит одним вызовом: раньше на каждое из 50 сообщений
        // приходился отдельный последовательный round-trip к клиенту.
        if (recentMessages.Count > 0)
        {
            var messageHistory = new ChatMessageSnapshot[recentMessages.Count];
            for (var messageIndex = 0; messageIndex < recentMessages.Count; ++messageIndex)
            {
                var recentMessage = recentMessages[messageIndex];
                messageHistory[messageIndex] = new ChatMessageSnapshot(
                    recentMessage.Identifier,
                    recentMessage.SenderName,
                    recentMessage.Text,
                    recentMessage.SentAtUtc);
            }

            await Clients.Caller.ReceiveMessageHistory(messageHistory);
        }

        await Clients.OthersInGroup(normalizedRoomName).ParticipantJoined(normalizedParticipantName);
    }

    /// <summary>Принимает сообщение, сохраняет его и рассылает всем участникам комнаты.</summary>
    public async Task SendMessage(string text)
    {
        var participantSession = TryGetParticipantSession()
            ?? throw new HubException("Сначала войдите в комнату.");

        var createdMessage = ChatMessage.Create(
            participantSession.RoomName,
            participantSession.ParticipantName,
            text);

        await chatMessageStore.SaveMessageAsync(createdMessage, Context.ConnectionAborted);

        await Clients.Group(participantSession.RoomName).ReceiveMessage(
            createdMessage.Identifier,
            createdMessage.SenderName,
            createdMessage.Text,
            createdMessage.SentAtUtc);

        // Не пишем каждое сообщение на уровне Information: даже асинхронный
        // консольный сток создаёт очередь, аллокации и лишний I/O на горячем пути.
        LogMessageDelivered(
            logger,
            createdMessage.Identifier,
            createdMessage.SenderName,
            participantSession.RoomName);
    }

    private ParticipantSession? TryGetParticipantSession() =>
        Context.Items.TryGetValue(ParticipantSessionItemKey, out var participantSession)
            ? participantSession as ParticipantSession
            : null;

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "Участник {ParticipantName} вошёл в комнату {RoomName} (подключение {ConnectionId})")]
    private static partial void LogParticipantJoined(
        ILogger logger,
        string participantName,
        string roomName,
        string connectionId);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Debug,
        Message = "Сообщение {MessageIdentifier} от {SenderName} доставлено в комнату {RoomName}")]
    private static partial void LogMessageDelivered(
        ILogger logger,
        Guid messageIdentifier,
        string senderName,
        string roomName);
}
