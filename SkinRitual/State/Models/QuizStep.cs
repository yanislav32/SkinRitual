﻿namespace SkinRitual.State.Models
{
    public enum QuizStep
    {
        None = 0,
        Role,           // 1. Ваша роль (фрилансер, сотрудник…)
        Experience,     // 2. Стаж / возраст
        Capital,        // 3. Объём капитала
        IncomeSources,  // 4. Кол-во источников дохода
        SpareMoney,     // 5. Есть ли «лишние» 10 % после расходов
        ExpenseTracking,// 6. Ведёте учёт трат?
        BudgetLeak,     // 7. Знаете «дырки» в бюджете?
        Reserve,        // 8. Накоплен резервной подушки?
        Goal,           // 9. Конкретная цель / срок
        Finished = 999,
        WaitingFullName = 1001, // ждём ФИО (ввод текстом)
        WaitingPhone = 1002  // ждём контакт (кнопкой)
    }
}
