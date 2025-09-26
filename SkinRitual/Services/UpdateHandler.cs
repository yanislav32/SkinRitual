using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkinRitual.Handlers;
using SkinRitual.State;
using SkinRitual.State.Models;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace SkinRitual.Services
{
    public sealed class UpdateHandler : IUpdateHandler
    {

        private readonly ITelegramBotClient _bot;
        private readonly StateService _states;
        private readonly List<IHandler> _handlers;
        private readonly IServiceProvider _provider;

        public UpdateHandler(ITelegramBotClient bot, StateService states, IEnumerable<IHandler> handlers, IServiceProvider provider)
        {
            _bot = bot;
            _states = states;
            _handlers = handlers.ToList();
            _provider = provider;
        }

        // ✅ новая сигнатура (Bot API v22) - без повторяющихся «_»
        public Task HandleErrorAsync(
         ITelegramBotClient botClient,
         Exception exception,
         HandleErrorSource source,
         CancellationToken ct)
        {
            Console.WriteLine($"TG error ({source}): {exception}");
            return Task.CompletedTask;
        }


        public async Task HandleUpdateAsync(
        ITelegramBotClient bot,
        Update update,
        CancellationToken ct)
        {
            Console.WriteLine($"▶ update: {update.Type}");//удалить позже

            // теперь НЕ фильтруем тип; отдаём всем зарегистрированным IHandler
            long chatId = update switch
            {
                { Message: { } m } => m.Chat.Id,
                { CallbackQuery: { } cb } => cb.Message!.Chat.Id,
                _ => 0
            };

            var state = chatId == 0 ? null : _states.Get(chatId);

            if (update.Message is { Type: MessageType.Text } msg && state is not null)
            {
                if (QuizHandler.DefaultMap.TryGetValue(state.Step, out var entry))
                {
                    var opts = entry.Opts;
                    if (!opts.Any(o => o.Trim().Equals(msg.Text.Trim(), StringComparison.OrdinalIgnoreCase)) && msg.Text != "/users_file" && msg.Text != "/users_feed" && msg.Text != "/start")
                        return;
                }
            }

            Console.WriteLine($"\n=== New update: Type={update.Type}, Chat={chatId}, Step={(state?.Step.ToString() ?? "null")} ===");
            if (update.Message != null)
                Console.WriteLine($"Message.Text: \"{update.Message.Text}\"");

            // ===== ШАГ ФИО =====
            if (state is not null && state.Step == QuizStep.WaitingFullName && update.Message?.Text is { } fioText)
            {
                var chat = chatId;
                var fio = fioText.Trim();

                // лёгкая проверка: не пусто и есть пробел (хотя бы имя+фамилия)
                if (string.IsNullOrWhiteSpace(fio) || !fio.Contains(' '))
                {
                    await _bot.SendMessage(chat,
                        "Пожалуйста, пришлите ФИО полностью (фамилия имя отчество).",
                        cancellationToken: ct);
                    return;
                }

                using var scope = _provider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SkinRitual.Data.BotDbContext>();

                // upsert: найдём или создадим
                var user = await db.Users.FindAsync(new object?[] { chat }, ct)
                           ?? new SkinRitual.State.Models.UserRecord
                           {
                               ChatId = chat,
                               UserName = update.Message!.From?.Username,
                               FirstSeen = DateTime.UtcNow
                           };

                user.FullName = fio; // перезаписываем при повторном проходе
                db.Update(user);
                await db.SaveChangesAsync(ct);

                // переходим к шагу телефона
                state.Step = QuizStep.WaitingPhone;
                _states.Save(chat, state);

                var kb = new ReplyKeyboardMarkup(new[]
                {
            new KeyboardButton[] { KeyboardButton.WithRequestContact("📱 Отправить номер телефона") }
        })
                {
                    ResizeKeyboard = true,
                    OneTimeKeyboard = true
                };

                await _bot.SendMessage(
                    chat,
                    "Спасибо! Теперь, пожалуйста, отправьте номер телефона кнопкой ниже:",
                    replyMarkup: kb,
                    cancellationToken: ct);

                return;
            }

            // ===== ШАГ ТЕЛЕФОНА (СТРОГО КНОПКОЙ) =====
            if (state is not null && state.Step == QuizStep.WaitingPhone)
            {
                var msgphone = update.Message;

                // разрешаем только contact; всё остальное (текст/фото/цифры) — игнор
                if (msgphone?.Contact is null)
                    return;

                // доп.проверка: принимаем только собственный контакт, отправленный самим юзером
                if (msgphone.From is not null && msgphone.Contact.UserId.HasValue && msgphone.Contact.UserId.Value != msgphone.From.Id)
                    return;

                var chat = chatId;
                using var scope = _provider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SkinRitual.Data.BotDbContext>();

                var user = await db.Users.FindAsync(new object?[] { chat }, ct)
                           ?? new SkinRitual.State.Models.UserRecord
                           {
                               ChatId = chat,
                               UserName = msgphone!.From?.Username,
                               FirstSeen = DateTime.UtcNow
                           };

                user.Phone = msgphone.Contact.PhoneNumber; // перезаписываем при повторном проходе
                db.Update(user);
                await db.SaveChangesAsync(ct);

                await _bot.SendMessage(
                    chat,
                    "✅ Ваш персональный финансовый план готов и активирован. Мы свяжемся с вами по указанному номеру.\n\n" +
                    $"ФИО: {user.FullName}\nТелефон: {user.Phone}\n\n" +
                    "<b>🙏 Спасибо за интерес и доверие!</b>\n\n" +
                    "Всё, что раньше было доступно только на закрытых консультациях, — теперь у вас под рукой. Воспользуйтесь этим ресурсом на максимум.\n\n" +
                    "До встречи в большом капитале!",
                    replyMarkup: new ReplyKeyboardRemove(),
                    parseMode: ParseMode.Html,
                    cancellationToken: ct);

                // сбрасываем состояние
                state.Step = QuizStep.None;
                _states.Save(chat, state);
                return;
            }

            foreach (var h in _handlers)
            {
                bool can = h.CanHandle(update, state!);
                Console.WriteLine($"  Handler {h.GetType().Name}.CanHandle → {can}");
                if (can)
                {
                    Console.WriteLine($"    → Invoking {h.GetType().Name}.HandleAsync");
                    await h.HandleAsync(bot, update, state!, _states, ct);
                    break;
                }
            }
        }
    }
}