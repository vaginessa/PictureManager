﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using PictureManager.Domain.Interfaces;
using PictureManager.Domain.Models;

namespace PictureManager.ViewModels.Tree {
  public sealed class DrivesTreeVM {
    private readonly AppCore _coreVM;

    public readonly Dictionary<int, DriveTreeVM> All = new();

    public DrivesTreeVM(AppCore coreVM) {
      _coreVM = coreVM;
    }

    public void SyncCollection(ObservableCollection<ITreeLeaf> src, ObservableCollection<ITreeLeaf> dest, ITreeBranch parent, Domain.Utils.Tree.OnItemsChanged onItemsChanged) {
      Domain.Utils.Tree.SyncCollection<FolderM, DriveTreeVM>(src, dest, parent,
        (model, treeVM) => treeVM.Model.Equals(model),
        model => Domain.Utils.Tree.GetDestItem(model, model.Id, All, () => ItemCreateVM(model, parent), onItemsChanged));
    }

    private DriveTreeVM ItemCreateVM(FolderM model, ITreeBranch parent) {
      var item = new DriveTreeVM(model, parent);
      item.OnExpandedChanged += (o, _) => _coreVM.FoldersTreeVM.HandleItemExpandedChanged(o as FolderTreeVM);

      return item;
    }
  }
}
