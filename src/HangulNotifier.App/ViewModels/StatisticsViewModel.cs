using DevExpress.Mvvm;
using HangulNotifier.Core.Rules;
using HangulNotifier.Data;

namespace HangulNotifier.App.ViewModels;

public sealed class RuleStatRow
{
    public string Description { get; init; } = "";
    public string Suggestion { get; init; } = "";
    public int Count { get; init; }
}

/// <summary>통계 대시보드 VM. 오늘/주/월 카운트, TOP10, 30일 추이.</summary>
public sealed class StatisticsViewModel : ViewModelBase
{
    private readonly IStatisticsRepository _repo;
    private readonly IReadOnlyList<Rule> _rules;

    public StatisticsViewModel(IStatisticsRepository repo)
    {
        _repo = repo;
        _rules = RuleSet.LoadDefault().Rules;
    }

    public int TodayCount { get => GetValue<int>(); set => SetValue(value); }
    public int WeekCount { get => GetValue<int>(); set => SetValue(value); }
    public int MonthCount { get => GetValue<int>(); set => SetValue(value); }
    public IReadOnlyList<RuleStatRow> TopRules { get => GetValue<IReadOnlyList<RuleStatRow>>(); set => SetValue(value); }
    public IReadOnlyList<DailyCount> DailySeries { get => GetValue<IReadOnlyList<DailyCount>>(); set => SetValue(value); }

    public void Refresh()
    {
        var now = DateTimeOffset.Now;
        var todayStart = new DateTimeOffset(now.Date, now.Offset);
        int backToMonday = now.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)now.DayOfWeek - 1;
        var weekStart = todayStart.AddDays(-backToMonday);
        var monthStart = new DateTimeOffset(new DateTime(now.Year, now.Month, 1), now.Offset);

        TodayCount = _repo.CountSince(todayStart);
        WeekCount = _repo.CountSince(weekStart);
        MonthCount = _repo.CountSince(monthStart);

        TopRules = _repo.TopRules(10, monthStart)
            .Select(rc =>
            {
                var rule = _rules.FirstOrDefault(r => r.Id == rc.RuleId);
                return new RuleStatRow
                {
                    Description = rule?.Message ?? rc.RuleId,
                    Suggestion = rule?.Suggestion ?? "",
                    Count = rc.Count,
                };
            })
            .ToList();

        DailySeries = _repo.DailySeries(30, now);
    }

    public void ClearAll()
    {
        _repo.ClearAll();
        Refresh();
    }
}
