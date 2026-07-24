using DevExpress.Mvvm;

namespace HangulNotifier.App.ViewModels;

/// <summary>설정 창의 규칙 개별 ON/OFF 항목.</summary>
public sealed class RuleToggle : BindableBase
{
    public string Id { get; init; } = "";
    public string Label { get; init; } = "";
    public bool Enabled { get => GetValue<bool>(); set => SetValue(value); }
}
