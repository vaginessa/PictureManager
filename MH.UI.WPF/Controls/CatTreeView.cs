﻿using MH.UI.WPF.Utils;
using MH.Utils;
using MH.Utils.BaseClasses;
using MH.Utils.Interfaces;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using static MH.Utils.DragDropHelper;

namespace MH.UI.WPF.Controls {
  public class CatTreeView : TreeView {
    public static readonly DependencyProperty ScrollToItemProperty = DependencyProperty.Register(
      nameof(ScrollToItem),
      typeof(ITreeItem),
      typeof(CatTreeView),
      new(ScrollToItemChanged));

    public ITreeItem ScrollToItem {
      get => (ITreeItem)GetValue(ScrollToItemProperty);
      set => SetValue(ScrollToItemProperty, value);
    }

    public static RelayCommand<ITreeItem> ItemCreateCommand { get; } = new(
      item => GetCategory(item)?.ItemCreate(item),
      item => item != null);

    public static RelayCommand<ITreeItem> ItemRenameCommand { get; } = new(
      item => GetCategory(item)?.ItemRename(item),
      item => item != null);

    public static RelayCommand<ITreeItem> ItemDeleteCommand { get; } = new(
      item => GetCategory(item)?.ItemDelete(item),
      item => item != null);

    public static RelayCommand<ITreeCategory> GroupCreateCommand { get; } = new(
      item => GetCategory(item)?.GroupCreate(item),
      item => item != null);

    public static RelayCommand<ITreeGroup> GroupRenameCommand { get; } = new(
      item => GetCategory(item)?.GroupRename(item),
      item => item != null);

    public static RelayCommand<ITreeGroup> GroupDeleteCommand { get; } = new(
      item => GetCategory(item)?.GroupDelete(item),
      item => item != null);

    private ScrollViewer _scrollViewer;
    private double _verticalOffset;

    public CanDragFunc CanDragFunc { get; }
    public CanDropFunc CanDropFunc { get; }
    public DoDropAction DoDropAction { get; }

    static CatTreeView() {
      DefaultStyleKeyProperty.OverrideMetadata(typeof(CatTreeView), new FrameworkPropertyMetadata(typeof(CatTreeView)));
    }

    public CatTreeView() {
      CanDragFunc = CanDrag;
      CanDropFunc = CanDrop;
      DoDropAction = DoDrop;
    }

    private static void ScrollToItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
      if (d is not CatTreeView ctv) return;
      ctv.ScrollTo((ITreeItem)e.NewValue);
      // reset so that scroll to same item is possible
      ctv.ScrollToItem = null;
    }

    private static ITreeCategory GetCategory(ITreeItem item) =>
      Tree.GetTopParent(item) as ITreeCategory;

    private void ScrollTo(ITreeItem item) {
      if (item == null) return;

      var items = new List<ITreeItem>();
      Tree.GetThisAndParentRecursive(item, ref items);
      items.Reverse();

      var offset = 0.0;
      var parent = this as ItemsControl;

      foreach (var treeItem in items) {
        var index = parent.Items.IndexOf(treeItem);
        var panel = parent.GetChildOfType<VirtualizingStackPanel>();
        if (panel == null) break;
        panel.BringIndexIntoViewPublic(index);
        if (parent.ItemContainerGenerator.ContainerFromIndex(index) is not TreeViewItem tvi) break;

        if (treeItem.Items.Count > 0)
          tvi.IsExpanded = true;

        parent = tvi;
        offset += panel.GetItemOffset(tvi);
      }

      _verticalOffset = offset;
      _scrollViewer?.ScrollToHorizontalOffset(0);
    }

    public override void OnApplyTemplate() {
      base.OnApplyTemplate();

      _scrollViewer = Template.FindName("PART_ScrollViewer", this) as ScrollViewer;

      LayoutUpdated += (_, _) => {
        if (_verticalOffset > 0) {
          _scrollViewer.ScrollToVerticalOffset(_verticalOffset);
          _verticalOffset = 0;
        }
      };
    }

    #region Drag & Drop
    private static object CanDrag(object source) {
      return source is ITreeCategory
        ? null
        : Tree.GetTopParent(source as ITreeItem) is not ITreeCategory
          ? null
          : source;
    }

    private MH.Utils.DragDropEffects CanDrop(object target, object data, bool haveSameOrigin) {
      var e = MH.UI.WPF.Utils.DragDropHelper.DragEventArgs;
      DragDropAutoScroll(e);

      var cat = Tree.GetTopParent(target as ITreeItem) as ITreeCategory;

      if (cat?.CanDrop(data, target as ITreeItem) == true) {
        if (target is ITreeGroup) return MH.Utils.DragDropEffects.Move;
        if (!cat.CanCopyItem && !cat.CanMoveItem) return MH.Utils.DragDropEffects.None;
        if (cat.CanCopyItem && (e.KeyStates & DragDropKeyStates.ControlKey) != 0) return MH.Utils.DragDropEffects.Copy;
        if (cat.CanMoveItem && (e.KeyStates & DragDropKeyStates.ControlKey) == 0) return MH.Utils.DragDropEffects.Move;
      }

      return MH.Utils.DragDropEffects.None;
    }

    private static void DoDrop(object data, bool haveSameOrigin) {
      var e = MH.UI.WPF.Utils.DragDropHelper.DragEventArgs;
      var tvi = Extensions.FindTemplatedParent<TreeViewItem>((FrameworkElement)e.OriginalSource);
      if (tvi?.DataContext is not ITreeItem dest ||
        Tree.GetTopParent(dest) is not ITreeCategory cat) return;

      var aboveDest = e.GetPosition(tvi).Y < tvi.ActualHeight / 2;
      cat.OnDrop(data, dest, aboveDest, (e.KeyStates & DragDropKeyStates.ControlKey) > 0);
    }

    /// <summary>
    /// Scroll TreeView when the mouse is near the top or bottom
    /// </summary>
    private void DragDropAutoScroll(DragEventArgs e) {
      var pos = e.GetPosition(this);
      if (pos.Y < 25)
        _scrollViewer.ScrollToVerticalOffset(_scrollViewer.VerticalOffset - 25);
      else if (ActualHeight - pos.Y < 25)
        _scrollViewer.ScrollToVerticalOffset(_scrollViewer.VerticalOffset + 25);
    }
    #endregion
  }
}
