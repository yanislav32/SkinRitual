using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SkinRitual.State;
using SkinRitual.State.Models;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace SkinRitual.Handlers
{
    internal sealed class CallbackHandler : IHandler
    {
        // Ловим только CallbackQuery
        public bool CanHandle(Update u, UserState _) =>
            u.CallbackQuery is not null;

        public async Task HandleAsync(
            ITelegramBotClient bot,
            Update u,
            UserState _,
            StateService __,
            CancellationToken ct)
        {
            // безопасно распаковываем
            if (u.CallbackQuery is null) return;
            var cb = u.CallbackQuery;

            long chat = cb.Message!.Chat.Id;

            if (cb.Data == "plan_get")
            {
                await bot.AnswerCallbackQuery(cb.Id, cancellationToken: ct);

                long chatplan = cb.Message!.Chat.Id;
                var st = __.Get(chat);
                st.Step = QuizStep.WaitingFullName;
                __.Save(chat, st);

                await bot.SendMessage(
                    chatplan,
                    "Пожалуйста, введите ваше ФИО (фамилия имя отчество) одним сообщением:",
                    cancellationToken: ct);

                return;
            }

            if (cb.Data is "ticket_Tue" or "ticket_Thu")
            {
                const string caption = """
Ваш осенний бьюти-бокс <b>забронирован! 😍🧡</b>

Мы свяжемся с вами по указанному номеру, чтобы уточнить детали доставки и выбора состава ✨

Спасибо за доверие. Вы сделали шаг в сторону <b>осознанного ухода</b> и маленьких ежедневных ритуалов, которые приносят коже <b>спокойствие и сияние 🙌🏻</b>

<b>Наслаждайтесь</b> этим моментом и позвольте себе чуть больше заботы.

🥰 До встречи в вашем <b>ритуале</b> красоты.
""";
                var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Ticket.png");

                await using var fs = File.OpenRead(path);
                await bot.SendPhoto(chat,
                    InputFile.FromStream(fs,
                    "Ticket.png"),
                    caption,
                    parseMode: ParseMode.Html);

                await bot.AnswerCallbackQuery(cb.Id, "Билет отправлен 👆");
            }
        }
    }
}