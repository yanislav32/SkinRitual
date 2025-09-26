using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SkinRitual.Data;
using SkinRitual.Handlers;
using SkinRitual.Services;
using SkinRitual.State;
using SkinRitual.State.Models;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((ctx, cfg) =>
    {
        // независимо от среды
        cfg.AddUserSecrets<Program>(optional: true);
    })
    .ConfigureServices((context, services) =>
    {
        // context.Configuration уже включает:
        // • appsettings*.json
        // • user-secrets (если среда Development)
        // • переменные окружения
        var cfg = context.Configuration;
        var conn = cfg.GetConnectionString("BotDb");
        //Console.WriteLine($"[DEBUG] TG_TOKEN = {cfg["TG_TOKEN"]}");

        var token = cfg["TG_TOKEN"]                         // User Secrets / appsettings
                 ?? Environment.GetEnvironmentVariable("TG_TOKEN") // env-var
                 ?? throw new InvalidOperationException("TG_TOKEN missing");

        services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(token));

        services.AddSingleton<StateService>();
        services.AddSingleton<ChecklistService>();

        services.AddSingleton<AdminAccess>();
        services.AddSingleton<IHandler, AdminHandler>();



        services.AddDbContext<BotDbContext>(opt =>
            opt.UseNpgsql(conn));

        // создаём единственный словарь map и шарим его
        var map = new Dictionary<QuizStep, (string, string[])>(QuizHandler.DefaultMap);
        services.AddSingleton(map);

        services.AddSingleton<IHandler, StartCommandHandler>();
        services.AddSingleton<IHandler, CallbackHandler>();
        services.AddSingleton<IHandler, QuizHandler>();

        services.AddSingleton<IUpdateHandler, UpdateHandler>();
        services.AddHostedService<BotBackgroundService>();
    })
    .Build();

using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();
    db.Database.Migrate();

    // сюда же помещаем "SetMyCommandsAsync"
    var bot = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();

    var commands = new[]
{
    new BotCommand { Command = "start", Description = "Запустить бота" }
};
    await bot.SetMyCommands(commands, scope: new BotCommandScopeDefault());

    // админские (добавим объединение с /start)
    var adminAccess = scope.ServiceProvider.GetRequiredService<SkinRitual.Services.AdminAccess>();
    if (adminAccess.Ids.Count > 0)
    {
        var defaultCommands = new[]
        {
        new BotCommand { Command = "start", Description = "Запустить бота" }
    };

        var adminExtras = new[]
        {
        new BotCommand { Command = "users_file", Description = "Экспорт пользователей (файл)" },
        new BotCommand { Command = "users_feed", Description = "Пользователи сообщениями" }
    };

        // 👇 объединяем: /start + админ-команды
        var adminCommands = defaultCommands
            .Concat(adminExtras)
            .GroupBy(c => c.Command) // на всякий, чтобы не задублилось
            .Select(g => g.First())
            .ToArray();

        foreach (var adminId in adminAccess.Ids)
        {
            try
            {
                await bot.SetMyCommands(
                    commands: adminCommands, // <-- тут уже есть и /start
                    scope: new BotCommandScopeChat { ChatId = new ChatId(adminId) },
                    cancellationToken: CancellationToken.None);
            }
            catch (ApiRequestException ex) when (ex.Message.Contains("chat not found", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[WARN] Admin {adminId}: chat not found (ещё не писал боту). Пропускаем назначение команд.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Не удалось назначить админ-команды для {adminId}: {ex}");
            }
        }
    }





    // 2) Делаем кнопку меню, которая в любой момент разворачивает список команд
    await bot.SetChatMenuButton(
            menuButton: new Telegram.Bot.Types.MenuButtonCommands(),
            cancellationToken: CancellationToken.None);
}

await host.RunAsync();
