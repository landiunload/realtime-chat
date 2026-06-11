using Microsoft.EntityFrameworkCore;
using RealtimeChat.WebApplication.Features.Messages;

namespace RealtimeChat.WebApplication.Persistence;

/// <summary>Контекст базы данных чата.</summary>
public sealed class ChatDatabaseContext(DbContextOptions<ChatDatabaseContext> contextOptions)
    : DbContext(contextOptions)
{
    /// <summary>Таблица сообщений.</summary>
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChatMessage>(chatMessageBuilder =>
        {
            chatMessageBuilder.ToTable("chat_messages");
            chatMessageBuilder.HasKey(chatMessage => chatMessage.Identifier);
            chatMessageBuilder.Property(chatMessage => chatMessage.RoomName).HasMaxLength(64).IsRequired();
            chatMessageBuilder.Property(chatMessage => chatMessage.SenderName).HasMaxLength(64).IsRequired();
            chatMessageBuilder.Property(chatMessage => chatMessage.Text)
                .HasMaxLength(ChatMessage.MaximumTextLength)
                .IsRequired();

            // Составной индекс под основной сценарий чтения: история конкретной комнаты по времени
            chatMessageBuilder.HasIndex(chatMessage => new { chatMessage.RoomName, chatMessage.SentAtUtc });
        });
    }
}
