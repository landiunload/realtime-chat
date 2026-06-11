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
}
