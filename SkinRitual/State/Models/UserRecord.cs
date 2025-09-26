﻿namespace SkinRitual.State.Models
{
    public class UserRecord
    {
        public long ChatId { get; set; }
        public string? UserName { get; set; }
        public DateTime FirstSeen { get; set; }
        public List<AnswerRecord> Answers { get; set; } = new();
        public string? FullName { get; set; }
        public string? Phone { get; set; }
    }
}
