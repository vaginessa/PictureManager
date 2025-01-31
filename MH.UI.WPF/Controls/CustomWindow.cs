﻿using System.Runtime.InteropServices;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using MH.Utils.BaseClasses;

namespace MH.UI.WPF.Controls {
  public class CustomWindow : Window {
    public static readonly DependencyProperty CanResizeProperty = DependencyProperty.Register(
      nameof(CanResize), typeof(bool), typeof(CustomWindow));

    public static readonly DependencyProperty IsFullScreenProperty = DependencyProperty.Register(
      nameof(IsFullScreen), typeof(bool), typeof(CustomWindow),
      new((o, _) => (o as CustomWindow)?.OnIsFullScreenChanged()));

    public static readonly DependencyProperty IsDragAreaForProperty = DependencyProperty.RegisterAttached(
      "IsDragAreaFor", typeof(Window), typeof(CustomWindow), new(OnIsDragAreaChanged));

    public bool CanResize {
      get => (bool)GetValue(CanResizeProperty);
      set => SetValue(CanResizeProperty, value);
    }

    public bool IsFullScreen {
      get => (bool)GetValue(IsFullScreenProperty);
      set => SetValue(IsFullScreenProperty, value);
    }

    public static Window GetIsDragAreaFor(DependencyObject d) => (Window)d.GetValue(IsDragAreaForProperty);
    public static void SetIsDragAreaFor(DependencyObject d, Window value) => d.SetValue(IsDragAreaForProperty, value);

    private const int _resizeCornerSize = 10;
    private const int _resizeBorderSize = 4;
    private const int WM_SYSCOMMAND = 0x112;

    private enum ResizeDirection {
      None = 0,
      Left = 61441,
      Right = 61442,
      Top = 61443,
      TopLeft = 61444,
      TopRight = 61445,
      Bottom = 61446,
      BottomLeft = 61447,
      BottomRight = 61448,
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    public static RelayCommand<Window> MinimizeWindowCommand { get; } = new(
      (window) => window.WindowState = WindowState.Minimized);

    public static RelayCommand<Window> MaximizeWindowCommand { get; } = new(
      (window) => {
        window.MaxHeight = double.PositiveInfinity;
        window.WindowState = WindowState.Maximized;
      });

    public static RelayCommand<Window> RestoreWindowCommand { get; } = new(
      (window) => window.WindowState = WindowState.Normal);

    public static RelayCommand<Window> CloseWindowCommand { get; } = new(
      (window) => window.Close());

    static CustomWindow() {
      DefaultStyleKeyProperty.OverrideMetadata(
        typeof(CustomWindow),
        new FrameworkPropertyMetadata(typeof(CustomWindow)));
    }

    public CustomWindow() {
      StateChanged += delegate { OnStateChanged(); };
      Loaded += delegate {
        if (WindowState == WindowState.Maximized) {
          var isFullScreen = IsFullScreen;
          WindowState = WindowState.Normal;
          if (isFullScreen)
            IsFullScreen = isFullScreen;
          WindowState = WindowState.Maximized;
        }
      };
    }

    public override void OnApplyTemplate() {
      base.OnApplyTemplate();

      if (Template.FindName("PART_ResizeBorder", this) is Border border) {
        border.MouseEnter += delegate { SetCursor(); };
        border.MouseLeave += delegate { ResetCursor(); };
        border.PreviewMouseLeftButtonDown += delegate { Resize(); };
      }
    }

    private void OnStateChanged() {
      if (WindowState == WindowState.Normal && IsFullScreen)
        IsFullScreen = false;

      if (WindowState == WindowState.Maximized && !IsFullScreen)
        MaxHeight = SystemParameters.WorkArea.Height;
    }

    public void OnIsFullScreenChanged() {
      MaxHeight = IsFullScreen
        ? double.PositiveInfinity
        : SystemParameters.WorkArea.Height;

      if (IsFullScreen && WindowState != WindowState.Maximized)
        WindowState = WindowState.Maximized;
    }

    private static void OnIsDragAreaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
      if (d is FrameworkElement fe && e.NewValue is Window window)
        fe.PreviewMouseLeftButtonDown += delegate { window.DragMove(); };
    }

    private void SetCursor() =>
      Cursor = ResizeDirectionToCursor(GetResizeDirection());

    private void ResetCursor() {
      if (Mouse.LeftButton != MouseButtonState.Pressed)
        Cursor = Cursors.Arrow;
    }

    private void Resize() {
      var hwndSource = (HwndSource)PresentationSource.FromVisual(this);
      var direction = GetResizeDirection();
      Cursor = ResizeDirectionToCursor(direction);
      SendMessage(hwndSource.Handle, WM_SYSCOMMAND, (IntPtr)direction, IntPtr.Zero);
    }

    private ResizeDirection GetResizeDirection() {
      var pos = Mouse.GetPosition(this);

      if (pos.X > _resizeCornerSize && pos.X < ActualWidth - _resizeCornerSize) {
        if (pos.Y < _resizeBorderSize) return ResizeDirection.Top;
        if (pos.Y > ActualHeight - _resizeBorderSize) return ResizeDirection.Bottom;
      }
      else if (pos.Y > _resizeCornerSize && pos.Y < ActualHeight - _resizeCornerSize) {
        if (pos.X < _resizeBorderSize) return ResizeDirection.Left;
        if (pos.X > ActualWidth - _resizeBorderSize) return ResizeDirection.Right;
      }
      else if (pos.X < _resizeCornerSize) {
        if (pos.Y < _resizeCornerSize) return ResizeDirection.TopLeft;
        if (pos.Y > ActualHeight - _resizeCornerSize) return ResizeDirection.BottomLeft;
      }
      else if (pos.X > ActualWidth - _resizeCornerSize) {
        if (pos.Y < _resizeCornerSize) return ResizeDirection.TopRight;
        if (pos.Y > ActualHeight - _resizeCornerSize) return ResizeDirection.BottomRight;
      }

      return ResizeDirection.None;
    }

    private static Cursor ResizeDirectionToCursor(ResizeDirection direction) =>
      direction switch {
        ResizeDirection.Left => Cursors.SizeWE,
        ResizeDirection.Right => Cursors.SizeWE,
        ResizeDirection.Top => Cursors.SizeNS,
        ResizeDirection.TopLeft => Cursors.SizeNWSE,
        ResizeDirection.TopRight => Cursors.SizeNESW,
        ResizeDirection.Bottom => Cursors.SizeNS,
        ResizeDirection.BottomLeft => Cursors.SizeNESW,
        ResizeDirection.BottomRight => Cursors.SizeNWSE,
        ResizeDirection.None => Cursors.Arrow,
        _ => Cursors.Arrow,
      };
  }
}
