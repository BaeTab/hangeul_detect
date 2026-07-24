using DevExpress.Xpf.Core;
using HangulNotifier.App.ViewModels;
using HangulNotifier.Data;

namespace HangulNotifier.App.Views;

public partial class StatisticsWindow : ThemedWindow
{
    private readonly StatisticsViewModel _vm;

    public StatisticsWindow(IStatisticsRepository repo)
    {
        InitializeComponent();
        _vm = new StatisticsViewModel(repo);
        DataContext = _vm;
    }

    public void RefreshData() => _vm.Refresh();

    private void ClearButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "모든 통계 데이터를 삭제할까요? 되돌릴 수 없습니다.",
            "전체 삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
            _vm.ClearAll();
    }
}
