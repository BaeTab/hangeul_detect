// DevExpress.Wpf가 WinForms 상호운용 참조를 끌어오므로, ImplicitUsings 환경에서
// System.Windows 와 System.Windows.Forms 타입이 충돌한다.
// WPF 타입으로 고정한다(트레이 앱에서 자주 쓰는 이름들).
global using Application = System.Windows.Application;
global using MessageBox = System.Windows.MessageBox;
global using Clipboard = System.Windows.Clipboard;
global using Cursors = System.Windows.Input.Cursors;
global using KeyEventArgs = System.Windows.Input.KeyEventArgs;
global using MouseEventArgs = System.Windows.Input.MouseEventArgs;
