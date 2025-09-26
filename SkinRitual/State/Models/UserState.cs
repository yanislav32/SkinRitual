using System.Collections.Generic;

namespace SkinRitual.State.Models
{
    public sealed class UserState
    {
        public QuizStep Step { get; set; } = QuizStep.None;
        public Dictionary<QuizStep, string> Answers { get; } = new();

        // NEW: временно держим введённое ФИО
        public string? TempFullName { get; set; }
    }
}
