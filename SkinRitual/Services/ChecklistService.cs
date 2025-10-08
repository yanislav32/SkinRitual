using System.Text.RegularExpressions;
using SkinRitual.State.Models;

namespace SkinRitual.Services
{
    public sealed class ChecklistService
    {
        /// <summary>Строит перечень сообщений; каждое ≤ 4096 симв.</summary>
        public string Build(IReadOnlyDictionary<QuizStep, string> a)
        {
            // ↓ ваш длинный исходный текст (можно сделать const string в Resources)
            const string raw = """
<b>Готова ĸ изменениям? Вот твой результат:😱</b>

<blockquote><i><u>Твои ответы показали главное: кожа живёт своим ритмом и просит внимания там, где мы чаще всего спешим.</u></i>

Для кого-то это сухие участки, которые тянутся после умывания. Для кого-то — покраснения, от которых сложно избавиться. А для кого-то — усталость и тусклость, когда отражение в зеркале не совпадает с тем, что хочется почувствовать.</blockquote>

Мы не предлагаем волшебной таблетки. Вместо этого — маленький ориентир, который возвращает уверенность: <b>гайд по уходу за кожей.</b> 🧡 В нём — простые, <b>бережные шаги:</b> несколько лёгких жестов, которые можно вплести в утро и вечер без перегруза.

Сохрани гайд так, <b>как удобно тебе:</b> можно распечатать и держать рядом с зеркалом, можно хранить в заметках и отмечать галочками важные шаги.

Главное — каждый день напоминать себе: <b>красота начинается с маленьких движений.🥰</b>
""";
            return  raw;
        }

        // ——— helpers ———
        private static IEnumerable<string> SplitSafe(string text, int limit)
        {
            if (text.Length <= limit) { yield return text; yield break; }

            var words = text.Split(' ');
            var sb = new List<string>();
            var len = 0;

            foreach (var w in words)
            {
                if (len + w.Length + 1 > limit)
                {
                    yield return string.Join(' ', sb);
                    sb.Clear(); len = 0;
                }
                sb.Add(w);
                len += w.Length + 1;
            }
            if (sb.Count > 0) yield return string.Join(' ', sb);
        }
    }
}
