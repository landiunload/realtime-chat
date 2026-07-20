using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RealtimeChat.WebApplication.Features.Messages;
using RealtimeChat.WebApplication.Hubs;
using Xunit;

namespace RealtimeChat.UnitTests;

/// <summary>
/// Тесты хаба чата. Главное, что здесь фиксируется, — история комнаты уходит
/// новому участнику одним вызовом, а не по вызову на каждое сообщение.
/// </summary>
public sealed class ChatHubTests
{
    private readonly IChatMessageStore _messageStoreSubstitute = Substitute.For<IChatMessageStore>();
    private readonly IChatClient _callerSubstitute = Substitute.For<IChatClient>();
    private readonly IChatClient _othersInGroupSubstitute = Substitute.For<IChatClient>();

    private ChatHub CreateHubUnderTest()
    {
        var hubCallerClients = Substitute.For<IHubCallerClients<IChatClient>>();
        hubCallerClients.Caller.Returns(_callerSubstitute);
        hubCallerClients.OthersInGroup(Arg.Any<string>()).Returns(_othersInGroupSubstitute);

        var hubCallerContext = Substitute.For<HubCallerContext>();
        hubCallerContext.ConnectionId.Returns("подключение-1");

        return new ChatHub(_messageStoreSubstitute, Substitute.For<ILogger<ChatHub>>())
        {
            Clients = hubCallerClients,
            Groups = Substitute.For<IGroupManager>(),
            Context = hubCallerContext
        };
    }

    private static IReadOnlyList<ChatMessage> CreateMessages(int messagesCount)
    {
        var createdMessages = new List<ChatMessage>(messagesCount);
        for (var messageNumber = 1; messageNumber <= messagesCount; ++messageNumber)
        {
            createdMessages.Add(ChatMessage.Create("general", "Андрей", $"Сообщение {messageNumber}"));
        }

        return createdMessages;
    }

    [Fact]
    public async Task JoinRoom_ЕстьИстория_ОтправляетЕёОднимВызовом()
    {
        // Подготовка
        var existingMessages = CreateMessages(50);
        _messageStoreSubstitute
            .FindRecentMessagesAsync("general", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(existingMessages);

        // Действие
        await CreateHubUnderTest().JoinRoom("general", "Андрей");

        // Проверка: ровно один вызов на всю историю, ни одного поштучного
        await _callerSubstitute.Received(1)
            .ReceiveMessageHistory(Arg.Is<IReadOnlyList<ChatMessageSnapshot>>(
                history => history.Count == existingMessages.Count));
        await _callerSubstitute.DidNotReceive().ReceiveMessage(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>());
    }

    [Fact]
    public async Task JoinRoom_ЕстьИстория_СохраняетХронологическийПорядок()
    {
        // Подготовка
        var existingMessages = CreateMessages(3);
        _messageStoreSubstitute
            .FindRecentMessagesAsync("general", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(existingMessages);

        IReadOnlyList<ChatMessageSnapshot>? capturedHistory = null;
        await _callerSubstitute.ReceiveMessageHistory(
            Arg.Do<IReadOnlyList<ChatMessageSnapshot>>(history => capturedHistory = history));

        // Действие
        await CreateHubUnderTest().JoinRoom("general", "Андрей");

        // Проверка
        Assert.NotNull(capturedHistory);
        Assert.Equal(
            existingMessages.Select(message => message.Text),
            capturedHistory.Select(snapshot => snapshot.Text));
    }

    [Fact]
    public async Task JoinRoom_ИсторииНет_НеОтправляетПустойПакет()
    {
        // Подготовка
        _messageStoreSubstitute
            .FindRecentMessagesAsync("general", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);

        // Действие
        await CreateHubUnderTest().JoinRoom("general", "Андрей");

        // Проверка
        await _callerSubstitute.DidNotReceive()
            .ReceiveMessageHistory(Arg.Any<IReadOnlyList<ChatMessageSnapshot>>());
        await _othersInGroupSubstitute.Received(1).ParticipantJoined("Андрей");
    }
}
