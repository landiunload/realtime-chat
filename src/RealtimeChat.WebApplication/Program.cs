using Microsoft.EntityFrameworkCore;
using RealtimeChat.WebApplication.Features.Messages;
using RealtimeChat.WebApplication.Hubs;
using RealtimeChat.WebApplication.Persistence;

var webApplicationBuilder = WebApplication.CreateBuilder(args);

webApplicationBuilder.Logging.ClearProviders();
webApplicationBuilder.Logging.AddJsonConsole(jsonConsoleFormatterOptions =>
{
    jsonConsoleFormatterOptions.TimestampFormat = "O";
    jsonConsoleFormatterOptions.UseUtcTimestamp = true;
});

var databaseConnectionString = webApplicationBuilder.Configuration.GetConnectionString("ChatDatabase")
    ?? throw new InvalidOperationException("Строка подключения «ChatDatabase» не найдена в конфигурации.");
var allowedHubOrigins = new HashSet<string>(
    webApplicationBuilder.Configuration
        .GetSection("Security:AllowedHubOrigins")
        .GetChildren()
        .Select(configurationSection => configurationSection.Value)
        .Where(static origin => !string.IsNullOrWhiteSpace(origin))
        .Select(static origin => origin!),
    StringComparer.OrdinalIgnoreCase);

webApplicationBuilder.Services.AddDbContextPool<ChatDatabaseContext>(
    databaseContextOptions => databaseContextOptions.UseNpgsql(databaseConnectionString),
    poolSize: 32);

webApplicationBuilder.Services.AddScoped<IChatMessageStore, PostgresChatMessageStore>();

webApplicationBuilder.Services.AddSignalR(signalROptions =>
{
    signalROptions.EnableDetailedErrors = false;
    signalROptions.MaximumParallelInvocationsPerClient = 1;
    signalROptions.MaximumReceiveMessageSize = 16 * 1024;
    signalROptions.StreamBufferCapacity = 10;
});

var webApplication = webApplicationBuilder.Build();

// Для демонстрации создаём схему базы при старте; в продакшене здесь были бы миграции
await EnsureDatabaseCreatedWithRetriesAsync(
    webApplication,
    webApplication.Lifetime.ApplicationStopping);

webApplication.Use(async (httpContext, nextMiddleware) =>
{
    if (httpContext.Request.Path.StartsWithSegments("/hubs/chat")
        && httpContext.Request.Headers.TryGetValue("Origin", out var requestOrigins)
        && requestOrigins.Count > 0
        && !allowedHubOrigins.Contains(requestOrigins.ToString()))
    {
        httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }

    // Хеши разрешают ровно два статических inline-блока из index.html, не весь
    // произвольный inline-код. После их изменения хеши нужно пересчитать.
    httpContext.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; "
        + "base-uri 'none'; object-src 'none'; frame-ancestors 'none'; form-action 'self'; "
        + "connect-src 'self'; img-src 'self' data:; "
        + "script-src 'self' https://cdnjs.cloudflare.com "
        + "'sha256-TH8AfHyBN47IyxjX1ZH4l2GoNLc1NnBbBO5ZiPfLueg='; script-src-attr 'none'; "
        + "style-src 'self' 'sha256-UryaRyT/d5b6k4KmDINxjhoEFsbJUci6UtjM8McEdrM='; style-src-attr 'none'";
    httpContext.Response.Headers["X-Content-Type-Options"] = "nosniff";
    httpContext.Response.Headers["Referrer-Policy"] = "no-referrer";
    httpContext.Response.Headers["Permissions-Policy"] =
        "camera=(), geolocation=(), microphone=(), payment=(), usb=()";

    await nextMiddleware();
});

webApplication.UseDefaultFiles();
webApplication.UseStaticFiles();

webApplication.MapHub<ChatHub>("/hubs/chat");
webApplication.MapGet("/health/live", static () => Results.NoContent());

webApplication.Run();

// База может быть ещё не готова: depends_on в docker compose стережёт только первый
// запуск, а контейнер приложения переживает перезапуски независимо от базы. Без
// повторов служба падала на старте и не поднималась, пока база не вернётся, — то есть
// кратковременная недоступность базы превращалась в постоянную недоступность чата.
// После исчерпания попыток падаем громко: значит дело не в задержке старта.
static async Task EnsureDatabaseCreatedWithRetriesAsync(
    WebApplication application,
    CancellationToken cancellationToken)
{
    const int maximumAttempts = 10;
    var delayBeforeNextAttempt = TimeSpan.FromSeconds(1);

    for (var attemptNumber = 1; ; ++attemptNumber)
    {
        try
        {
            await using var startupServiceScope = application.Services.CreateAsyncScope();
            var chatDatabaseContext = startupServiceScope.ServiceProvider
                .GetRequiredService<ChatDatabaseContext>();
            await chatDatabaseContext.Database.EnsureCreatedAsync(cancellationToken);
            return;
        }
        catch (Exception databaseException) when (
            !cancellationToken.IsCancellationRequested
            && attemptNumber < maximumAttempts)
        {
            StartupLog.DatabaseUnavailable(
                application.Logger,
                attemptNumber,
                maximumAttempts,
                delayBeforeNextAttempt,
                databaseException);

            await Task.Delay(delayBeforeNextAttempt, cancellationToken);

            // Нарастающая задержка с потолком: не выжигаем попытки за первые секунды,
            // но и не растягиваем старт до бесконечности.
            delayBeforeNextAttempt = TimeSpan.FromSeconds(
                Math.Min(delayBeforeNextAttempt.TotalSeconds * 2, 15));
        }
    }
}

internal static partial class StartupLog
{
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Warning,
        Message = "База данных недоступна (попытка {AttemptNumber} из {MaximumAttempts}), повтор через {RetryDelay}")]
    public static partial void DatabaseUnavailable(
        ILogger logger,
        int attemptNumber,
        int maximumAttempts,
        TimeSpan retryDelay,
        Exception exception);
}
