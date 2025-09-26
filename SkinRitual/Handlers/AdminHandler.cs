using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SkinRitual.Data;
using SkinRitual.Services;
using SkinRitual.State;
using SkinRitual.State.Models;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace SkinRitual.Handlers
{
    internal sealed class AdminHandler : IHandler
    {
        private readonly AdminAccess _admins;
        private readonly BotDbContext _db;

        public AdminHandler(AdminAccess admins, BotDbContext db)
        {
            _admins = admins;
            _db = db;
        }

        public bool CanHandle(Update u, UserState _)
            => u.Message is { Type: MessageType.Text, Text: not null } m
               && (m.Text.Equals("/users_file", StringComparison.OrdinalIgnoreCase)
                   || m.Text.Equals("/users_feed", StringComparison.OrdinalIgnoreCase));

        public async Task HandleAsync(
            ITelegramBotClient bot,
            Update u,
            UserState state,
            StateService states,
            CancellationToken ct)
        {
            var msg = u.Message!;
            var fromId = msg.From?.Id ?? 0;

            // доступ только для админов
            if (!_admins.IsAdmin(fromId))
                return;

            if (msg.Text!.Equals("/users_file", StringComparison.OrdinalIgnoreCase))
            {
                await SendUsersFile(bot, msg.Chat.Id, ct);
                return;
            }

            if (msg.Text!.Equals("/users_feed", StringComparison.OrdinalIgnoreCase))
            {
                await SendUsersFeed(bot, msg.Chat.Id, ct);
                return;
            }
        }


        private async Task SendUsersFile(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            var users = await _db.Users
                .OrderBy(u => u.FirstSeen)
                .ToListAsync(ct);

            var sb = new StringBuilder();
            int i = 1;
            foreach (var u in users)
            {
                var usernameBlock = string.IsNullOrWhiteSpace(u.UserName) ? "—" : $"@{u.UserName}";
                sb.AppendLine($"#{i++}");
                sb.AppendLine($"Username: {usernameBlock}");
                sb.AppendLine($"ФИО: {u.FullName ?? "—"}");
                sb.AppendLine($"Телефон: {u.Phone ?? "—"}");
                sb.AppendLine($"Подключился: {u.FirstSeen:dd.MM.yyyy HH:mm:ss}");
                sb.AppendLine(new string('-', 32));
            }

            var fileName = $"users_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt";
            var tempPath = Path.Combine(Path.GetTempPath(), fileName);
            await File.WriteAllTextAsync(tempPath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), ct);

            await using var fs = File.OpenRead(tempPath);
            await bot.SendDocument(
                chatId,
                InputFile.FromStream(fs, fileName), // ✅ этот тип доступен через Telegram.Bot.Types
                caption: $"Всего пользователей: {users.Count}",
                cancellationToken: ct
            );
        }


        private static string H(string? s) => WebUtility.HtmlEncode(s ?? "—");

        private async Task SendUsersFeed(ITelegramBotClient bot, long chatId, CancellationToken ct)
        {
            var users = await _db.Users
                .OrderBy(u => u.FirstSeen)
                .ToListAsync(ct);

            foreach (var u in users)
            {
                string usernameHtml = "—";
                if (!string.IsNullOrWhiteSpace(u.UserName))
                {
                    var uname = WebUtility.HtmlEncode(u.UserName);
                    usernameHtml = $@"<a href=""https://t.me/{uname}"">@{uname}</a>";
                }

                var text =
        $@"<b>Пользователь</b>
Username: {usernameHtml}
ФИО: {H(u.FullName)}
Телефон: {H(u.Phone)}
Подключился: {u.FirstSeen:dd.MM.yyyy HH:mm:ss}";

                await bot.SendMessage(
                    chatId,
                    text,
                    parseMode: ParseMode.Html,
                    cancellationToken: ct
                );

                await Task.Delay(60, ct); // лёгкий троттлинг
            }

            if (users.Count == 0)
            {
                await bot.SendMessage(chatId, "Пока нет пользователей.", cancellationToken: ct);
            }
        }

    }
}
