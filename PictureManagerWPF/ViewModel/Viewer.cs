﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace PictureManager.ViewModel {
  public class Viewer : BaseTreeViewTagItem {
    public DataModel.PmDataContext Db;

    public BaseTreeViewItem IncludedFolders;
    public BaseTreeViewItem ExcludedFolders;

    private readonly AppCore aCore;

    public Viewer() {
      IncludedFolders = new BaseTreeViewItem {Title = "Included Folders", IconName = "appbar_folder_star", Parent = this};
      ExcludedFolders = new BaseTreeViewItem {Title = "Excluded Folders", IconName = "appbar_folder_star", Parent = this};

      Items.Add(IncludedFolders);
      Items.Add(ExcludedFolders);

      IconName = "appbar_eye";

      aCore = (AppCore) Application.Current.Properties["AppCore"];
    }

    public Viewer(DataModel.PmDataContext db, DataModel.Viewer data) : this() {
      Db = db;
      DbData = data;
      Id = data.Id;
      Title = data.Name;

      LoadFolders(true);
      LoadFolders(false);
    }

    private void LoadFolders(bool included) {
      (included ? IncludedFolders : ExcludedFolders).Items.Clear();
      
      var dirs =
        from va in Db.ViewersAccess.Where(x => x.IsIncluded == included && x.ViewerId == Id && x.DirectoryId != null)
        join d in Db.Directories on va.DirectoryId equals d.Id
        orderby d.Path
        select new KeyValuePair<DataModel.ViewerAccess, DataModel.Directory>(va, d);

      foreach (var dir in dirs) {
        var folder = InitFolder(dir.Key, included, dir.Value.Path);
        (included ? IncludedFolders : ExcludedFolders).Items.Add(folder);
      }
    }

    private BaseTreeViewItem InitFolder(DataModel.ViewerAccess data, bool included, string path) {
      return new BaseTreeViewItem {
        DbData = data,
        IconName = "appbar_folder",
        Title = GetTitleFromPath(path),
        ToolTip = path,
        Parent = included ? IncludedFolders : ExcludedFolders,
      };
    }

    public void AddFolder(bool included, string path) {
      var dmViewerAccess = new DataModel.ViewerAccess {
        Id = Db.GetNextIdFor("ViewersAccess"),
        ViewerId = Id,
        IsIncluded = included,
        DirectoryId = aCore.InsertDirecotryInToDb(path)
      };

      Db.InsertOnSubmit(dmViewerAccess);
      SetInPlace(InitFolder(dmViewerAccess, included, path));
    }

    private static void SetInPlace(BaseTreeViewItem item) {
      item.Parent.Items.Add(item);
      var idx = item.Parent.Items.OrderBy(x => x.ToolTip).ToList().IndexOf(item);
      item.Parent.Items.Move(item.Parent.Items.IndexOf(item), idx);
    }

    private static string GetTitleFromPath(string path) {
      path = path.EndsWith("\\") ? path.Substring(0, path.Length - 1) : path;
      var lio = path.LastIndexOf("\\", StringComparison.OrdinalIgnoreCase);
      return path.Substring(lio + 1, path.Length - lio - 1);
    }
  }
}
