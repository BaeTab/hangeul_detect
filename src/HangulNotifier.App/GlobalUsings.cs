// ImplicitUsings를 끄고(_wpftmp 버그 회피) 기본 네임스페이스를 여기서 명시한다.
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using System.Windows;

// WPF 타입 고정 (DevExpress가 System.Drawing/WinForms를 끌어와 충돌)
global using Application = System.Windows.Application;
global using MessageBox = System.Windows.MessageBox;
global using Clipboard = System.Windows.Clipboard;
global using Cursors = System.Windows.Input.Cursors;
global using KeyEventArgs = System.Windows.Input.KeyEventArgs;
global using MouseEventArgs = System.Windows.Input.MouseEventArgs;
global using Brush = System.Windows.Media.Brush;
global using Brushes = System.Windows.Media.Brushes;
global using Color = System.Windows.Media.Color;
global using Colors = System.Windows.Media.Colors;
global using ColorConverter = System.Windows.Media.ColorConverter;
global using SolidColorBrush = System.Windows.Media.SolidColorBrush;
global using FontFamily = System.Windows.Media.FontFamily;
global using Point = System.Windows.Point;
global using Size = System.Windows.Size;
