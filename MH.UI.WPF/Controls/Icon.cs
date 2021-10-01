﻿using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MH.UI.WPF.Controls {
  public class Icon : Control {
    public static readonly DependencyProperty SourceProperty =
      DependencyProperty.Register(nameof(Source), typeof(PathGeometry), typeof(Icon));
    public static readonly DependencyProperty FillProperty =
      DependencyProperty.Register(nameof(Fill), typeof(Brush), typeof(Icon));
    public static readonly DependencyProperty SizeProperty =
     DependencyProperty.Register(nameof(Size), typeof(double), typeof(Icon), new PropertyMetadata(18.0));
    public static readonly DependencyProperty IsIconVisibleProperty =
      DependencyProperty.Register(nameof(IsIconVisible), typeof(bool), typeof(Icon), new PropertyMetadata(true));

    public PathGeometry Source {
      get => (PathGeometry)GetValue(SourceProperty);
      set => SetValue(SourceProperty, value);
    }

    public Brush Fill {
      get => (Brush)GetValue(FillProperty);
      set => SetValue(FillProperty, value);
    }

    public double Size {
      get => (double)GetValue(SizeProperty);
      set => SetValue(SizeProperty, value);
    }

    public bool IsIconVisible {
      get => (bool)GetValue(IsIconVisibleProperty);
      set => SetValue(IsIconVisibleProperty, value);
    }

    static Icon() {
      DefaultStyleKeyProperty.OverrideMetadata(typeof(Icon), new FrameworkPropertyMetadata(typeof(Icon)));
    }
  }
}
