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
await EnsureDatabaseCreatedWithRetriesAsync(webApplication);

webApplication.UseDefaultFiles();
webApplication.UseStaticFiles();

webApplication.MapHub<ChatHub>("/hubs/chat");

webApplication.Run();

// База может быть ещё не готова: depends_on в docker compose стережёт только первый
// запуск, а контейнер приложения переживает перезапуски независимо от базы. Без
// повторов служба падала на старте и не поднималась, пока база не вернётся, — то есть
// кратковременная недоступность базы превращалась в постоянную недоступность чата.
// После исчерпания попыток падаем громко: значит дело не в задержке старта.
static async Task EnsureDatabaseCreatedWithRetriesAsync(WebApplication application)
{
    const int maximumAttempts = 10;
    var delayBeforeNextAttempt = TimeSpan.FromSeconds(1);

    for (var attemptNumber = 1; ; ++attemptNumber)
    {
        try
        {
            using var startupServiceScope = application.Services.CreateScope();
            var chatDatabaseContext = startupServiceScope.ServiceProvider
                .GetRequiredService<ChatDatabaseContext>();
            await chatDatabaseContext.Database.EnsureCreatedAsync();
            return;
        }
        catch (Exception databaseException) when (attemptNumber < maximumAttempts)
        {
            application.Logger.LogWarning(
                databaseException,
                "База данных недоступна (попытка {НомерПопытки} из {ВсегоПопыток}), повтор через {Задержка}",
                attemptNumber,
                maximumAttempts,
                delayBeforeNextAttempt);

            await Task.Delay(delayBeforeNextAttempt);

            // Нарастающая задержка с потолком: не выжигаем попытки за первые секунды,
            // но и не растягиваем старт до бесконечности.
            delayBeforeNextAttempt = TimeSpan.FromSeconds(
                Math.Min(delayBeforeNextAttempt.TotalSeconds * 2, 15));
        }
    }
}
