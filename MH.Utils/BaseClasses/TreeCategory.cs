﻿using MH.Utils.EventsArgs;
using MH.Utils.Interfaces;
using System;

namespace MH.Utils.BaseClasses {
  public class TreeCategory : TreeItem, ITreeCategory {
    public event EventHandler<TreeItemDoppedEventArgs> AfterDropEventHandler = delegate { };
    public bool CanCopyItem { get; set; }
    public bool CanMoveItem { get; set; }

    public TreeCategory(string iconName, string name) : base(iconName, name) { }

    public virtual void ItemCreate(ITreeItem root) => throw new NotImplementedException();
    public virtual void ItemRename(ITreeItem item) => throw new NotImplementedException();
    public virtual void ItemDelete(ITreeItem item) => throw new NotImplementedException();
    public virtual void ItemCopy(ITreeItem item, ITreeItem dest) => throw new NotImplementedException();
    public virtual void ItemMove(ITreeItem item, ITreeItem dest, bool aboveDest) => throw new NotImplementedException();

    public virtual void GroupCreate(ITreeItem root) => throw new NotImplementedException();
    public virtual void GroupRename(ITreeGroup group) => throw new NotImplementedException();
    public virtual void GroupDelete(ITreeGroup group) => throw new NotImplementedException();
    public virtual void GroupMove(ITreeGroup group, ITreeGroup dest, bool aboveDest) => throw new NotImplementedException();

    public virtual bool CanDrop(object src, ITreeItem dest) => CanDrop(src as ITreeItem, dest);

    public virtual void OnDrop(object src, ITreeItem dest, bool aboveDest, bool copy) {
      // groups
      if (src is ITreeGroup srcGroup && dest is ITreeGroup destGroup) {
        GroupMove(srcGroup, destGroup, aboveDest);
        return;
      }

      // items
      if (src is ITreeItem srcItem && dest != null)
        if (copy)
          ItemCopy(srcItem, dest);
        else
          ItemMove(srcItem, dest, aboveDest);

      AfterDropEventHandler(this, new(src, dest, aboveDest, copy));
    }

    public static bool CanDrop(ITreeItem src, ITreeItem dest) {
      if (src == null || dest == null || Equals(src, dest) ||
          src.Parent.Equals(dest) || dest.Parent?.Equals(src) == true ||
          (src is ITreeGroup && dest is not ITreeGroup)) return false;

      // if src or dest categories are null or they are not equal
      if (Tree.GetTopParent(src) is not ITreeCategory srcCat ||
          Tree.GetTopParent(dest) is not ITreeCategory destCat ||
          !Equals(srcCat, destCat)) return false;

      return true;
    }
  }
}
