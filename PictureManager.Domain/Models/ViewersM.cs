﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using MH.Utils.BaseClasses;
using MH.Utils.Extensions;
using MH.Utils.Interfaces;
using PictureManager.Domain.DataAdapters;
using PictureManager.Domain.EventsArgs;
using SimpleDB;

namespace PictureManager.Domain.Models {
  public sealed class ViewersM : ObservableObject, ITreeBranch {
    #region ITreeBranch implementation
    public ITreeBranch Parent { get; set; }
    public ObservableCollection<ITreeLeaf> Items { get; set; } = new();
    #endregion

    private readonly Core _core;
    private ViewerM _current;

    public DataAdapter DataAdapter { get; }
    public List<ViewerM> All { get; } = new();
    public ViewerM Current { get => _current; set { _current = value; OnPropertyChanged(); } }

    public event EventHandler<ViewerDeletedEventArgs> ViewerDeletedEvent = delegate { };

    public ViewersM(Core core) {
      _core = core;
      DataAdapter = new ViewersDataAdapter(core, this);
    }

    public ViewerM ItemCreate(ITreeBranch root, string name) {
      var item = new ViewerM(DataAdapter.GetNextId(), name, root);
      root.Items.SetInOrder(item, x => ((ViewerM)x).Name);
      All.Add(item);
      DataAdapter.IsModified = true;

      return item;
    }

    public bool ItemCanRename(string name) =>
      !All.Any(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public void ItemRename(ViewerM item, string name) {
      item.Name = name;
      item.Parent.Items.SetInOrder(item, x => ((ViewerM)x).Name);
      DataAdapter.IsModified = true;
    }

    public void ItemDelete(ViewerM viewer) {
      viewer.Parent.Items.Remove(viewer);
      viewer.Parent = null;
      viewer.IncludedFolders.Clear();
      viewer.ExcludedFolders.Clear();
      viewer.ExcludedKeywords.Clear();
      All.Remove(viewer);
      ViewerDeletedEvent(this, new(viewer));
      DataAdapter.IsModified = true;
    }

    public void ToggleCategoryGroup(ViewerM viewer, int groupId) {
      viewer.ExcCatGroupsIds.Toggle(groupId);
      DataAdapter.IsModified = true;
    }

    public void AddFolder(ViewerM viewer, FolderM folder, bool included) {
      (included ? viewer.IncludedFolders : viewer.ExcludedFolders).SetInOrder(folder, (x) => x.FullPath);
      DataAdapter.IsModified = true;
    }

    public void RemoveFolder(ViewerM viewer, FolderM folder, bool included) {
      (included ? viewer.IncludedFolders : viewer.ExcludedFolders).Remove(folder);
      DataAdapter.IsModified = true;
    }

    public void AddKeyword(ViewerM viewer, KeywordM keyword) {
      viewer.ExcludedKeywords.SetInOrder(keyword, (x) => x.FullName);
      DataAdapter.IsModified = true;
    }

    public void RemoveKeyword(ViewerM viewer, KeywordM keyword) {
      viewer.ExcludedKeywords.Remove(keyword);
      DataAdapter.IsModified = true;
    }

    public void SetCurrent(ViewerM viewer) {
      if (Current != viewer) {
        if (Current != null)
          Current.IsDefault = false;

        viewer.IsDefault = true;
        DataAdapter.Save();
        Current = viewer;
      }

      Current.UpdateHashSets();
      _core.FoldersM.AddDrives();
      _core.FolderKeywordsM.Load();
      _core.CategoryGroupsM.UpdateVisibility(Current);
    }

    public bool CanViewerSee(FolderM folder) =>
      Current?.CanSee(folder) != false;

    public bool CanViewerSeeContentOf(FolderM folder) =>
      Current?.CanSeeContentOf(folder) != false;

    public bool CanViewerSee(MediaItemM mediaItem) =>
      Current?.CanSee(mediaItem) != false;
  }
}
