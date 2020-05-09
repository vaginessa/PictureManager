﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using PictureManager.Domain;
using PictureManager.Properties;
using PictureManager.Utils;

namespace PictureManager.Dialogs {
  /// <summary>
  /// Interaction logic for FileOperationCollisionDialog.xaml
  /// </summary>
  public partial class FileOperationCollisionDialog: INotifyPropertyChanged {
    public event PropertyChangedEventHandler PropertyChanged;

    public void OnPropertyChanged([CallerMemberName] string name = null) {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private readonly List<string> _tempThumbs = new List<string>();
    private bool _error;
    private string _fileName;
    private FileInfo _srcFileInfo;
    private FileInfo _destFileInfo;

    public bool Error { get => _error; set { _error = value; OnPropertyChanged(); } }
    public string FileName { get => _fileName; set { _fileName = value; OnPropertyChanged(); } }
    public FileInfo SrcFileInfo { get => _srcFileInfo; set { _srcFileInfo = value; OnPropertyChanged(); } }
    public FileInfo DestFileInfo { get => _destFileInfo; set { _destFileInfo = value; OnPropertyChanged(); } }
    public Uri SrcFilePathCacheUri => GetThumbFilePath(SrcFileInfo.FullName).Result;
    public Uri DestFilePathCacheUri => GetThumbFilePath(DestFileInfo.FullName).Result;
    public string SrcFileSize => $"File size: {SrcFileInfo.Length} B";
    public string DestFileSize => $"File size: {DestFileInfo.Length} B";
    public string SrcFileModified => $"Modified: {SrcFileInfo.LastWriteTime}";
    public string DestFileModified => $"Modified: {DestFileInfo.LastWriteTime}";
    public string SrcDimensions => GetDimensions(SrcFileInfo.FullName);
    public string DestDimensions => GetDimensions(DestFileInfo.FullName);

    public CollisionResult Result;

    public FileOperationCollisionDialog(string srcFilePath, string destFilePath, Window owner) {
      SrcFileInfo = new FileInfo(srcFilePath);
      DestFileInfo = new FileInfo(destFilePath);
      FileName = SrcFileInfo.Name;
      Owner = owner;
      Result = CollisionResult.Skip;
      InitializeComponent();
    }

    private static string GetDimensions(string filePath) {
      var size = Imaging.GetImageDimensionsAsync(filePath).Result;
      return size == null ? string.Empty : $"Dimensions: {size[0]} x {size[1]}";
    }

    private async Task<Uri> GetThumbFilePath(string filePath) {
      var thumbPath = filePath.Replace(Path.VolumeSeparatorChar.ToString(), Settings.Default.CachePath);
      if (!File.Exists(thumbPath)) {
        _tempThumbs.Add(thumbPath);
        await Imaging.CreateThumbnailAsync(Domain.Utils.Imaging.GetMediaType(filePath), filePath, thumbPath,
          Settings.Default.ThumbnailSize);
      }

      return new Uri(thumbPath);
    }

    private void BtnRename_OnClick(object sender, RoutedEventArgs e) {
      Error = false;
      TxtFileName.ToolTip = string.Empty;

      if (string.IsNullOrEmpty(FileName)) {
        Error = true;
        return;
      }

      if (Path.GetInvalidFileNameChars().Any(FileName.Contains)) {
        TxtFileName.ToolTip = "New file name contains incorrect character(s)!";
        Error = true;
        return;
      }

      var newFilePath = Path.Combine(DestFileInfo.DirectoryName, FileName);
      if (File.Exists(newFilePath)) {
        DestFileInfo = new FileInfo(newFilePath);
        OnPropertyChanged(nameof(DestFilePathCacheUri));
        OnPropertyChanged(nameof(DestFileSize));
        OnPropertyChanged(nameof(DestFileModified));
        return;
      }

      Result = CollisionResult.Rename;
      Close();
    }

    private void BtnReplace_OnClick(object sender, RoutedEventArgs e) {
      Result = CollisionResult.Replace;
      Close();
    }

    private void BtnSkip_OnClick(object sender, RoutedEventArgs e) {
      Result = CollisionResult.Skip;
      Close();
    }

    private void OnClosing(object sender, CancelEventArgs e) {
      foreach (var tempThumb in _tempThumbs) {
        if (File.Exists(tempThumb))
          File.Delete(tempThumb);
      }
    }
  }
}
