﻿using MH.Utils;
using MH.Utils.Interfaces;
using PictureManager.Domain.Models;
using System.Collections.Generic;
using System.Linq;

namespace PictureManager.Domain.DataAdapters {
  /// <summary>
  /// DB fields: ID|Name|Parent
  /// </summary>
  public class FoldersDataAdapter : DataAdapter<FolderM> {
    private readonly FoldersM _model;

    public FoldersDataAdapter(FoldersM model) : base("Folders", 3) {
      _model = model;
    }

    public static IEnumerable<T> GetAll<T>(ITreeItem root) {
      yield return (T)root;

      foreach (var item in root.Items)
        foreach (var subItem in GetAll<T>(item))
          if (!FoldersM.FolderPlaceHolder.Equals(subItem))
            yield return subItem;
    }

    public override void Save() =>
      SaveDriveRelated(_model.Items.ToDictionary(x => x.Name, GetAll<FolderM>));

    public override FolderM FromCsv(string[] csv) =>
      string.IsNullOrEmpty(csv[2])
        ? new DriveM(int.Parse(csv[0]), csv[1], null)
        : new FolderM(int.Parse(csv[0]), csv[1], null);

    public override string ToCsv(FolderM folder) =>
      string.Join("|",
        folder.GetHashCode().ToString(),
        folder.Name,
        (folder.Parent as FolderM)?.GetHashCode().ToString() ?? string.Empty);

    public override void LinkReferences() {
      _model.Items.Clear();

      foreach (var (folder, csv) in AllCsv) {
        // reference to Parent and back reference from Parent to SubFolder
        folder.Parent = !string.IsNullOrEmpty(csv[2])
          ? AllDict[int.Parse(csv[2])]
          : _model;
        folder.Parent.Items.Add(folder);
      }
    }
  }
}
