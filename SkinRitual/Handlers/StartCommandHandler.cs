using System.IO;
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
    internal sealed class StartCommandHandler : IHandler
    {
        private readonly Dictionary<QuizStep, (string Q, string[] Opts)> _map;
        private readonly ChecklistService _chk;   // понадобится, если решите сдвигать дальше
        private readonly BotDbContext _db;
        private readonly AdminAccess _admins;

        public StartCommandHandler(Dictionary<QuizStep, (string, string[])> map,
                                   ChecklistService chk, BotDbContext db, AdminAccess admins)
        {
            _map = map;
            _chk = chk;
            _db = db;
            _admins = admins;
        }

        public bool CanHandle(Update u, UserState _) => u.Message?.Text == "/start";

        public async Task HandleAsync(
            ITelegramBotClient bot,
            Update u,
            UserState state,
            StateService states,
            CancellationToken ct)
        {
            long chat = u.Message!.Chat.Id;

            // -1) чистим предыдущее состояние
            states.Reset(chat);
            state = states.Get(chat);

            // 0) Сохраняем или обновляем UserRecord:
            var user = await _db.Users.FindAsync(chat);
            if (user == null)
            {
                user = new UserRecord
                {
                    ChatId = chat,
                    UserName = u.Message.From?.Username,
                    FirstSeen = DateTime.UtcNow
                };
                _db.Users.Add(user);

                string userTgLink = "https://t.me/{user.UserName}";
                var msgNewUser =
                                $"<b>Новый пользователь!</b>\n" +
                                $"Username: <a href=\"https://t.me/{user.UserName}\">@{user.UserName}</a>\n" +
                                $"Id: <code>{user.ChatId}</code>\n" +
                                $"Дата подключения: {user.FirstSeen:dd-MM-yyyy}\n" +
                                $"Время подключения: {user.FirstSeen:HH:mm:ss}";
                long adminChatId = 528017102;
                await bot.SendMessage(adminChatId, msgNewUser, parseMode: ParseMode.Html);
                await bot.SendMessage(406865885, msgNewUser, parseMode: ParseMode.Html);

            }
            else if (user.FirstSeen == default)
            {
                user.FirstSeen = DateTime.UtcNow;
                _db.Users.Update(user);
            }
            await _db.SaveChangesAsync(ct);


            // 1) приветственный текст
            const string welcome = """
<b>Добро пожаловать в PoreOver!🧡</b>

Мы создаём уход, где <b>на первом месте</b> — доĸазанная эффеĸтивность ичестные формулы. Каждое средство рождается из праĸтиĸи и научного подхода, чтобы дарить ĸоже результат и ощущение споĸойствия.

<i><u>PoreOver — это:</u></i>

<blockquote>🌿 Униĸальные составы, собранные из проверенных аĸтивов и современных технологий.
🔬 Абсолютная прозрачность: мы объясняем, ĸаĸ работает ĸаждый ингредиент.
✨ Минимализм, ĸоторый эĸономит ресурсы и время: меньше продуĸтов, больше пользы.
🤍 Ритуал заботы, в ĸотором ĸожа чувствует благодарность, а вы — уверенность.</blockquote>

PoreOver выбирают те, ĸто <b>ценит ĸрасоту</b> без иллюзий, <b>честность</b> без ĸомпромиссов и уход, ĸоторый действительно <b>работает.</b>
""";
            await bot.SendMessage(chat, welcome, parseMode: ParseMode.Html, cancellationToken: ct);

            // 2) PDF-презентация + пояснение
            const string more = """
Мы верим, что уход за кожей — это не набор баночек в ванной, а часть жизни. Здесь учат чувствовать комфорт, а не гнаться за «идеалом». Помогают не бояться реактивности кожи. Показывают, что уход работает и в простых жестах — в текстурах, ритуалах, <b>маленьких моментах тишины.</b>

В Pore Over вы найдёте пространство, где вас слышат, поддерживают и подбирают решения под вашу кожу. Без громких обещаний, без иллюзий, зато с наукой, честностью и результатом. Добро пожаловать. ✨
""";
            /*var pdf = Path.Combine(AppContext.BaseDirectory, "Assets", "Presentation.pdf");
            await bot.SendDocument(
                chat,
                InputFile.FromStream(File.OpenRead(pdf), "Presentation.pdf"),
                more,
                parseMode: ParseMode.Html,
                cancellationToken: ct);
            */

            // 3) voice
            var voice = Path.Combine(AppContext.BaseDirectory, "Assets", "welcome.ogg");
            await bot.SendVoice(
                chat,
                InputFile.FromStream(File.OpenRead(voice), "welcome.ogg"),
                /*more,
                parseMode: ParseMode.Html,*/
                cancellationToken: ct);


            // 4) сразу запускаем квиз
            _ = Task.Run(async () => 
            {
                await Task.Delay(TimeSpan.FromSeconds(15), ct);
                state.Step = QuizStep.Role;
                states.Save(chat, state);
                var (q, opts) = _map[QuizStep.Role];
                await bot.SendMessage(chat, q,
                    parseMode: ParseMode.Html,
                    replyMarkup: BuildReply(opts), cancellationToken: ct);
            });
        }

        private static ReplyMarkup BuildReply(string[] opts) =>
            new ReplyKeyboardMarkup(opts.Select(o => new[] { new KeyboardButton(o) }))
            { ResizeKeyboard = true, OneTimeKeyboard = true };
    }
}
