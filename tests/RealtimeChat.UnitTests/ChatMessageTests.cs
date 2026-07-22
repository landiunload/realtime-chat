using RealtimeChat.WebApplication.Features.Messages;
using Xunit;

namespace RealtimeChat.UnitTests;

/// <summary>Тесты фабричного метода сообщения чата.</summary>
public sealed class ChatMessageTests
{
    [Fact]
    public void Create_КорректныеДанные_СоздаётСообщениеСОбрезаннымиПробелами()
    {
        // Действие
        var createdMessage = ChatMessage.Create("general", "  Андрей  ", "  Привет!  ");

        // Проверка
        Assert.Equal("Андрей", createdMessage.SenderName);
        Assert.Equal("Привет!", createdMessage.Text);
        Assert.NotEqual(Guid.Empty, createdMessage.Identifier);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ПустойТекст_ВыбрасываетИсключение(string emptyText)
    {
        Assert.ThrowsAny<ArgumentException>(() => ChatMessage.Create("general", "Андрей", emptyText));
    }

    [Fact]
    public void Create_СлишкомДлинныйТекст_ВыбрасываетИсключение()
    {
        // Подготовка
        var textExceedingMaximumLength = new string('а', ChatMessage.MaximumTextLength + 1);

        // Действие и проверка
        Assert.Throws<ArgumentException>(() => ChatMessage.Create("general", "Андрей", textExceedingMaximumLength));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ПустоеНазваниеКомнаты_ВыбрасываетИсключение(string emptyRoomName)
    {
        Assert.ThrowsAny<ArgumentException>(() => ChatMessage.Create(emptyRoomName, "Андрей", "Привет!"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ПустоеИмяОтправителя_ВыбрасываетИсключение(string emptySenderName)
    {
        Assert.ThrowsAny<ArgumentException>(() => ChatMessage.Create("general", emptySenderName, "Привет!"));
    }

    [Fact]
    public void Create_ТекстРовноМаксимальнойДлины_СоздаётСообщение()
    {
        // Граничное значение: ровно предел допустим, проверка отсекает только «больше»
        var textAtMaximumLength = new string('а', ChatMessage.MaximumTextLength);

        var createdMessage = ChatMessage.Create("general", "Андрей", textAtMaximumLength);

        Assert.Equal(ChatMessage.MaximumTextLength, createdMessage.Text.Length);
    }

    [Fact]
    public void Create_НазваниеКомнатыСПробелами_ОбрезаетПробелы()
    {
        var createdMessage = ChatMessage.Create("  general  ", "Андрей", "Привет!");

        Assert.Equal("general", createdMessage.RoomName);
    }

    // Длина названия комнаты и имени отправителя ограничена схемой: обе колонки
    // объявлены varchar(64). Проверок на это в фабрике не было, поэтому длинное
    // имя доходило до вставки и падало DbUpdateException'ом уже в хранилище —
    // клиент вместо внятного отказа получал ошибку сервера. Текст с самого начала
    // проверялся против своего предела, эти два поля просто забыли.

    [Fact]
    public void Create_СлишкомДлинноеНазваниеКомнаты_ВыбрасываетИсключение()
    {
        var roomNameExceedingMaximumLength = new string('к', ChatMessage.MaximumNameLength + 1);

        Assert.Throws<ArgumentException>(
            () => ChatMessage.Create(roomNameExceedingMaximumLength, "Андрей", "Привет!"));
    }

    [Fact]
    public void Create_СлишкомДлинноеИмяОтправителя_ВыбрасываетИсключение()
    {
        var senderNameExceedingMaximumLength = new string('и', ChatMessage.MaximumNameLength + 1);

        Assert.Throws<ArgumentException>(
            () => ChatMessage.Create("general", senderNameExceedingMaximumLength, "Привет!"));
    }

    [Fact]
    public void Create_ИменаРовноМаксимальнойДлины_СоздаётСообщение()
    {
        // Граница включительно: ровно предел схема принимает, отсекается только «больше»
        var nameAtMaximumLength = new string('к', ChatMessage.MaximumNameLength);

        var createdMessage = ChatMessage.Create(nameAtMaximumLength, nameAtMaximumLength, "Привет!");

        Assert.Equal(ChatMessage.MaximumNameLength, createdMessage.RoomName.Length);
        Assert.Equal(ChatMessage.MaximumNameLength, createdMessage.SenderName.Length);
    }

    [Fact]
    public void Create_ИмяИзПробеловИДлинногоХвоста_ПроверяетДлинуПослеОбрезки()
    {
        // Обрезка идёт до проверки длины: имя, укладывающееся в предел после
        // отбрасывания пробелов, отвергать не за что — в базу поедет обрезанное
        var paddedName = "  " + new string('к', ChatMessage.MaximumNameLength) + "  ";

        var createdMessage = ChatMessage.Create(paddedName, "Андрей", "Привет!");

        Assert.Equal(ChatMessage.MaximumNameLength, createdMessage.RoomName.Length);
    }
}
