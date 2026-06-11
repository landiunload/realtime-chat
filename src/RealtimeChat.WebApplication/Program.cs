using Microsoft.EntityFrameworkCore;
using RealtimeChat.WebApplication.Features.Messages;
using RealtimeChat.WebApplication.Hubs;
using RealtimeChat.WebApplication.Persistence;
using Serilog;

var webApplicationBuilder = WebApplication.CreateBuilder(args);

webApplicationBuilder.Host.UseSerilog((hostBuilderContext, loggerConfiguration) =>
    loggerConfiguration.ReadFrom.Configuration(hostBuilderContext.Configuration));

var databaseConnectionString = webApplicationBuilder.Configuration.GetConnectionString("ChatDatabase")
    ?? throw new InvalidOperationException("Строка подключения «ChatDatabase» не найдена в конфигурации.");

webApplicationBuilder.Services.AddDbContext<ChatDatabaseContext>(databaseContextOptions =>
    databaseContextOptions.UseNpgsql(databaseConnectionString));

webApplicationBuilder.Services.AddScoped<IChatMessageStore, PostgresChatMessageStore>();

webApplicationBuilder.Services.AddSignalR();

var webApplication = webApplicationBuilder.Build();

// Для демонстрации создаём схему базы при старте; в продакшене здесь были бы миграции
using (var startupServiceScope = webApplication.Services.CreateScope())
{
    var chatDatabaseContext = startupServiceScope.ServiceProvider.GetRequiredService<ChatDatabaseContext>();
    await chatDatabaseContext.Database.EnsureCreatedAsync();
}

webApplication.UseDefaultFiles();
webApplication.UseStaticFiles();

webApplication.MapHub<ChatHub>("/hubs/chat");

webApplication.Run();
