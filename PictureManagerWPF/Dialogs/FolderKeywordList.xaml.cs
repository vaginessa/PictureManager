﻿using PictureManager.Domain.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;

namespace PictureManager.Dialogs {
  public partial class FolderKeywordList : INotifyPropertyChanged {
    public event PropertyChangedEventHandler PropertyChanged;
    public void OnPropertyChanged([CallerMemberName] string name = null) =>
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public ObservableCollection<Folder> Items { get; } = new();

    public FolderKeywordList() {
      foreach (var folder in App.Core.Folders.All.Cast<Folder>().Where(x => x.IsFolderKeyword).OrderBy(x => x.FullPath)) {
        Items.Add(folder);
      }

      InitializeComponent();
    }

    private void BtnRemove_OnClick(object sender, RoutedEventArgs e) {
      if (LbFolderKeywords.SelectedItems.Count == 0) return;
      if (!MessageDialog.Show("Remove Confirmation", "Are you sure?", true)) return;

      foreach (var item in LbFolderKeywords.SelectedItems.Cast<Folder>().ToList()) {
        item.IsFolderKeyword = false;
        Items.Remove(item);
      }

      App.Core.Folders.DataAdapter.IsModified = true;
      App.Core.FolderKeywords.Load();
    }
  }
}
