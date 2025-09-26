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
    internal sealed class QuizHandler : IHandler
    {
        // ❶ Карта «шаг → (вопрос, кнопки)»
        public static readonly Dictionary<QuizStep, (string Q, string[] Opts)> DefaultMap = new()
        {
            [QuizStep.Role] = (
    """
<b>💎 Финансовая диагностика, которая обычно стоит $1000 — сегодня бесплатно</b>

Почему большинство стратегий не работают? Не из-за рынка, а потому что их строят вслепую, без диагностики.

Статистика говорит сама за себя:

<blockquote>
— 7 из 10 тратят больше, чем планировали
— 8 из 10 не имеют чёткого финансового резерва
— Почти никто не использует потенциал своих денег на 100%
</blockquote>

Всего за пару минут вы ответите на девять вопросов, которые покажут: где ваши деньги недорабатывают, на чём вы теряете, и насколько устойчива ваша система. 

<i><u>📥 Начинаем прямо сейчас:</u></i>

Как бы вы описали свою роль?
""".Trim(),
                new[] { "Предприниматель", "Руководитель", "Специалист", "Другое" }),

            [QuizStep.Experience] = ("Опыт инвестиций?", new[] { "Начинающий", "1–3 года", "3+ лет" }),
            [QuizStep.Capital] = ("Свободный капитал, которым готовы управлять?", new[] { "< 1 млн ₽", "1-5 млн ₽", "5+ млн ₽" }),
            [QuizStep.IncomeSources] = ("Сколько у вас источников дохода?", new[] { "1", "2-3", "4+" }),
            [QuizStep.SpareMoney] = ("«Лишние деньги» за месяц чаще…", new[] { "Инвестирую", "Лежат", "Растворяются" }),
            [QuizStep.ExpenseTracking] = ("Учёт расходов ведёте?", new[] { "Да, регулярно", "Иногда", "Нет" }),
            [QuizStep.BudgetLeak] = ("Что сильнее «съедает» бюджет?", new[] { "Кредиты", "Спонтанные покупки", "Бизнес-расходы" }),
            [QuizStep.Reserve] = ("Резерв покрывает…", new[] { "< 3 мес", "3-5 мес", "6+ мес" }),
            [QuizStep.Goal] = ("Главная цель на год?", new[] { "Увеличить доход", "Снизить долги", "Накопить резерв" }),
        };

        private readonly Dictionary<QuizStep, (string Q, string[] Opts)> _map;
        private readonly ChecklistService _checklist;
        private readonly BotDbContext _db;

        public QuizHandler(Dictionary<QuizStep, (string, string[])> map, ChecklistService checklist, BotDbContext db)
        {
            _map = map;
            _checklist = checklist;
            _db = db;
        }

        public bool CanHandle(Update u, UserState s) =>
            u.Message is { Type: MessageType.Text } &&
            s.Step is >= QuizStep.Role and < QuizStep.Finished;  // FIX

        public async Task HandleAsync(
            ITelegramBotClient bot,
            Update u,
            UserState state,
            StateService states,
            CancellationToken ct)
        {

            long chat = u.Message!.Chat.Id;
            var prevStep = state.Step;
            string answer = u.Message.Text!.Trim();

            // сравните ответ с вариантами, как вы уже делали …
            if (!_map[state.Step].Opts.Any(o => o.Trim().Equals(answer, StringComparison.OrdinalIgnoreCase)))
                return;  // не кнопка — игнор

            var rec = new AnswerRecord
            {
                ChatId = chat,
                Step = prevStep,
                Response = answer,
                AnsweredAt = DateTime.UtcNow
            };
            _db.Answers.Add(rec);
            await _db.SaveChangesAsync(ct);

            // сохраняем ответ и увеличиваем шаг
            state.Answers[prevStep] = answer;
            state.Step = Next(prevStep);

            // ── вот здесь сохраняем изменившийся state ─────────────────────────
            states.Save(chat, state);   // или Reset+Get, см. выше
                                        // ──────────────────────────────────────────────────────────────────

            // дальше идёт обработка Finished / отправка следующего вопроса…

            // ── если опрос окончен ───────────────────────────────────────────────
            if (state.Step == QuizStep.Finished)
            {
                var checklist = _checklist.Build(state.Answers);
                await bot.SendMessage(chat, checklist, parseMode: ParseMode.Html, cancellationToken: ct);

                var pdf = Path.Combine(AppContext.BaseDirectory, "Assets", "Checklist.pdf");
                await using var fs = File.OpenRead(pdf);

                await bot.SendDocument(chat,
                    InputFile.FromStream(fs, "Checklist.pdf"),
                    "Ваш чек-лист (PDF)",
                    cancellationToken: ct);                            // FIX

                // ── приглашение через 2 мин (можно вернуть задержку) ────────────
                _ = Task.Run(async () =>
                {
                    // await Task.Delay(TimeSpan.FromMinutes(2), ct);

                    const string invite = """
<b>🎟 Ваша финансовая стратегия начинается здесь — получите её прямо сейчас</b>

Вы уже знаете, где ваш капитал недорабатывает и куда утекают деньги. Следующий шаг — получить индивидуальный финансовый план, который покажет, как усилить доходность, сократить потери и создать прочную подушку безопасности.

<i><u>В вашем финансовом плане вы получите:</u></i>

1. Анализ и диагностика всех ваших текущих финансов
2. Расчёт доходов и расходов — где утекает, где можно усилить
3. Постановку персональных финансовых целей
4. Индивидуальную инвестиционную стратегию
5. Рекомендации по налоговой оптимизации
6. Планирование крупных покупок и будущих обязательств
7. Контроль и сопровождение на каждом этапе

<blockquote>— Преумножать капитал,
— Создать стабильный пассивный доход,
— Сформировать пенсионный или образовательный капитал,
— Копить на крупные цели (например, квартиру).
</blockquote>

Все рекомендации — не из шаблона, а на основе ваших данных.

⏳ Пока доступно бесплатно — не откладывайте.
""";

                    var kb = new InlineKeyboardMarkup(new[]
                    {
                        new [] { InlineKeyboardButton.WithCallbackData("Получить финансовый план", "plan_get") }
                    });

                    await bot.SendMessage(chat, invite,
                        parseMode: ParseMode.Html,                     // FIX
                        replyMarkup: kb, cancellationToken: ct);
                });

                states.Reset(chat);
                return;
            }

            // ── шлём следующий вопрос ───────────────────────────────────────────
            var (q, opts) = _map[state.Step];
            await bot.SendMessage(chat, q,                      // FIX
                parseMode: ParseMode.Html,
                replyMarkup: BuildReply(opts), cancellationToken: ct);
        }

        private static QuizStep Next(QuizStep step) =>
            step == QuizStep.Goal ? QuizStep.Finished : (QuizStep)((int)step + 1);

        private static ReplyMarkup BuildReply(string[] opts) =>
            opts.Length == 0
                ? new ReplyKeyboardRemove()
                : new ReplyKeyboardMarkup(opts.Select(o => new[] { new KeyboardButton(o) }))
                { ResizeKeyboard = true, OneTimeKeyboard = true };
    }
}
