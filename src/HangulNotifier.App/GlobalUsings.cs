// DevExpress.Wpf가 WinForms 상호운용 참조를 끌어오므로, ImplicitUsings 환경에서
// System.Windows 와 System.Windows.Forms 타입이 충돌한다.
// WPF 타입으로 고정한다(트레이 앱에서 자주 쓰는 이름들).
global using Application = System.Windows.Application;
global using MessageBox = System.Windows.MessageBox;
global using Clipboard = System.Windows.Clipboard;
global using Cursors = System.Windows.Input.Cursors;
global using KeyEventArgs = System.Windows.Input.KeyEventArgs;
global using MouseEventArgs = System.Windows.Input.MouseEventArgs;

// WPF 미디어 타입 고정 (DevExpress가 System.Drawing을 끌어와 충돌)
global using Brush = System.Windows.Media.Brush;
global using Brushes = System.Windows.Media.Brushes;
global using Color = System.Windows.Media.Color;
global using Colors = System.Windows.Media.Colors;
global using ColorConverter = System.Windows.Media.ColorConverter;
global using SolidColorBrush = System.Windows.Media.SolidColorBrush;
global using FontFamily = System.Windows.Media.FontFamily;
global using Point = System.Windows.Point;
global using Size = System.Windows.Size;
