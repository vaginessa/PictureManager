﻿using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace PictureManager.Data {
  public class Folders : BaseItem {
    public ObservableCollection<Folder> Items { get; set; }

    public Folders() {
      Items = new ObservableCollection<Folder>();
    }

    public void AddDrives() {
      string[] drives = Environment.GetLogicalDrives();

      foreach (string drive in drives) {
        DriveInfo di = new DriveInfo(drive);
        string driveImage;

        switch (di.DriveType) {
          case DriveType.CDRom:
            driveImage = "appbar_cd";
            break;
          case DriveType.Network:
            driveImage = "appbar_drive";
            break;
          case DriveType.NoRootDirectory:
          case DriveType.Unknown:
            driveImage = "appbar_drive_error";
            break;
          default:
            driveImage = "appbar_drive";
            break;
        }
        Folder item = new Folder {
          Title = $"{(di.IsReady ? di.VolumeLabel : di.DriveType.ToString())} ({di.Name})",
          FullPath = drive,
          IconName = driveImage,
          IsAccessible = di.IsReady
        };

        if (di.IsReady)
          item.Items.Add(new Folder {Title = "..."});

        Items.Add(item);
      }
    }

    public Folder ExpandTo(string fullPath) {
      ObservableCollection<Folder> items = Items;
      while (true) {
        var folder = items.FirstOrDefault(f => fullPath.StartsWith(f.FullPath, StringComparison.OrdinalIgnoreCase));
        if (folder == null) return null;
        if (folder.Items.Count != 0) folder.IsExpanded = true;
        if (fullPath.Equals(folder.FullPath, StringComparison.OrdinalIgnoreCase)) return folder;
        items = folder.Items;
      }
    }
  }
}
