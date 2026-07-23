using Microsoft.EntityFrameworkCore;
using RealtimeChat.WebApplication.Features.Messages;
using RealtimeChat.WebApplication.Persistence;
using Xunit;

namespace RealtimeChat.UnitTests;

/// <summary>
/// Тесты хранилища сообщений на InMemory-провайдере EF Core. Проверяется главный
/// сценарий чтения из README: выбрать последние сообщения комнаты и отдать их в
/// хронологическом порядке. Хаб в своих тестах подменяет хранилище заглушкой,
/// поэтому сортировка, <c>Take</c> и разворот в <c>FindRecentMessagesAsync</c> до
/// сих пор не были покрыты ничем: удаление <c>Reverse()</c>, разворот сортировки
/// или сломанный фильтр по комнате не роняли ни одного теста.
///
/// Почему InMemory, а не SQLite или PostgreSQL: запрос сортирует по
/// <c>DateTimeOffset</c>, а SQLite такой <c>ORDER BY</c> не переводит; поднимать
/// же PostgreSQL ради модульного теста избыточно. InMemory исполняет операторы
/// <c>Where</c>/<c>OrderByDescending</c>/<c>Take</c>/<c>Reverse</c> теми же
/// семантиками, что выражает запрос, — покрывается именно логика, которая раньше
/// не проверялась. Порядок задаётся строго различными метками времени (все в UTC):
/// их упорядочение совпадает с тем, что даст PostgreSQL. Разрешение совпадений по
/// идентификатору здесь намеренно не проверяется — оно зависит от провайдера (uuid
/// у PostgreSQL против сравнения <c>Guid</c> у InMemory), и тест на нём проверял бы
/// поведение провайдера, а не хранилища.
/// </summary>
public sealed class PostgresChatMessageStoreTests
{
    // Своя изолированная InMemory-база на экземпляр теста: xUnit создаёт новый
    // экземпляр под каждый тест, поэтому уникальное имя разводит их данные.
    private readonly DbContextOptions<ChatDatabaseContext> _contextOptions =
        new DbContextOptionsBuilder<ChatDatabaseContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

    // Фиксированная точка отсчёта: метки задаются относительно неё, чтобы порядок
    // сообщений не зависел от системных часов и был строго детерминированным.
    private static readonly DateTimeOffset BaseInstant = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    // Create() ставит SentAtUtc = UtcNow, а тесту нужны строго различные, заранее
    // известные метки. Переопределяем момент отправки через приватный сеттер —
    // тест управляет только временем, не ослабляя инварианты модели.
    private static ChatMessage MessageAt(string roomName, string text, int secondsFromBase)
    {
        var chatMessage = ChatMessage.Create(roomName, "Андрей", text);
        typeof(ChatMessage)
            .GetProperty(nameof(ChatMessage.SentAtUtc))!
            .SetValue(chatMessage, BaseInstant.AddSeconds(secondsFromBase));
        return chatMessage;
    }

    private async Task SeedAsync(params ChatMessage[] chatMessages)
    {
        await using var databaseContext = new ChatDatabaseContext(_contextOptions);
        databaseContext.ChatMessages.AddRange(chatMessages);
        await databaseContext.SaveChangesAsync();
    }

    private async Task<IReadOnlyList<ChatMessage>> FindRecentAsync(string roomName, int messagesCount)
    {
        await using var databaseContext = new ChatDatabaseContext(_contextOptions);
        var messageStore = new PostgresChatMessageStore(databaseContext);
        return await messageStore.FindRecentMessagesAsync(roomName, messagesCount, CancellationToken.None);
    }

    [Fact]
    public async Task FindRecentMessagesAsync_ЕстьЧужиеКомнаты_ВозвращаетТолькоЗапрошенную()
    {
        // Подготовка: между сообщениями нужной комнаты вклинивается чужое
        await SeedAsync(
            MessageAt("general", "g1", 1),
            MessageAt("random", "r1", 2),
            MessageAt("general", "g2", 3));

        // Действие
        var recentMessages = await FindRecentAsync("general", 50);

        // Проверка: чужая комната отфильтрована, свои — в хронологическом порядке
        Assert.All(recentMessages, message => Assert.Equal("general", message.RoomName));
        Assert.Equal(["g1", "g2"], recentMessages.Select(message => message.Text));
    }

    [Fact]
    public async Task FindRecentMessagesAsync_БольшеЧемЛимит_ВозвращаетПоследниеNВХронологическомПорядке()
    {
        // Подготовка: пять сообщений по возрастанию времени
        await SeedAsync(
            MessageAt("general", "m1", 1),
            MessageAt("general", "m2", 2),
            MessageAt("general", "m3", 3),
            MessageAt("general", "m4", 4),
            MessageAt("general", "m5", 5));

        // Действие: просим только три последних
        var recentMessages = await FindRecentAsync("general", 3);

        // Проверка: именно три новейших (m3,m4,m5) и именно в прямом, а не в
        // обратном порядке. Ловит пропавший Reverse, сортировку не в ту сторону
        // и неверный Take одним ожиданием.
        Assert.Equal(["m3", "m4", "m5"], recentMessages.Select(message => message.Text));
    }

    [Fact]
    public async Task FindRecentMessagesAsync_МеньшеЧемЛимит_ВозвращаетВсёХронологически()
    {
        // Подготовка
        await SeedAsync(
            MessageAt("general", "первое", 1),
            MessageAt("general", "второе", 2));

        // Действие
        var recentMessages = await FindRecentAsync("general", 50);

        // Проверка: возвращены оба сообщения в порядке отправки
        Assert.Equal(["первое", "второе"], recentMessages.Select(message => message.Text));
    }

    [Fact]
    public async Task FindRecentMessagesAsync_ВКомнатеНетСообщений_ВозвращаетПустойСписок()
    {
        // Подготовка: сообщение есть, но в другой комнате
        await SeedAsync(MessageAt("general", "g1", 1));

        // Действие
        var recentMessages = await FindRecentAsync("empty-room", 50);

        // Проверка
        Assert.Empty(recentMessages);
    }
}
