using System.Threading;
using System.Threading.Tasks;
using SkinRitual.State;
using SkinRitual.State.Models;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace SkinRitual.Handlers
{
    /// <summary>Базовый интерфейс любого обработчика апдейтов.</summary>
    public interface IHandler
    {
        bool CanHandle(Update update, UserState state);

        Task HandleAsync(
            ITelegramBotClient bot,
            Update update,
            UserState state,
            StateService states,
            CancellationToken ct);
    }
}
