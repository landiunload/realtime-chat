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
public sealed class ChatHub(
    IChatMessageStore chatMessageStore,
    ILogger<ChatHub> logger) : Hub<IChatClient>
{
    private const int RecentMessagesCountToSendOnJoin = 50;

    /// <summary>Подключает участника к комнате и отправляет ему последние сообщения.</summary>
    public async Task JoinRoom(string roomName, string participantName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, roomName);

        logger.LogInformation(
            "Участник {ИмяУчастника} вошёл в комнату {НазваниеКомнаты} (подключение {ИдентификаторПодключения})",
            participantName,
            roomName,
            Context.ConnectionId);

        // Новому участнику — история, остальным — уведомление о входе
        var recentMessages = await chatMessageStore.FindRecentMessagesAsync(
            roomName,
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

        await Clients.OthersInGroup(roomName).ParticipantJoined(participantName);
    }

    /// <summary>Принимает сообщение, сохраняет его и рассылает всем участникам комнаты.</summary>
    public async Task SendMessage(string roomName, string senderName, string text)
    {
        // Фабричный метод выбросит исключение при некорректных данных —
        // SignalR вернёт его вызывающему клиенту
        var createdMessage = ChatMessage.Create(roomName, senderName, text);

        await chatMessageStore.SaveMessageAsync(createdMessage, Context.ConnectionAborted);

        await Clients.Group(roomName).ReceiveMessage(
            createdMessage.Identifier,
            createdMessage.SenderName,
            createdMessage.Text,
            createdMessage.SentAtUtc);

        logger.LogInformation(
            "Сообщение {ИдентификаторСообщения} от {ИмяОтправителя} доставлено в комнату {НазваниеКомнаты}",
            createdMessage.Identifier,
            createdMessage.SenderName,
            roomName);
    }
}
