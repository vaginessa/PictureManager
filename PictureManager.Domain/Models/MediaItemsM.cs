﻿using MH.Utils;
using MH.Utils.BaseClasses;
using MH.Utils.Dialogs;
using MH.Utils.Extensions;
using MH.Utils.Interfaces;
using PictureManager.Domain.DataAdapters;
using PictureManager.Domain.Dialogs;
using PictureManager.Domain.HelperClasses;
using PictureManager.Domain.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PictureManager.Domain.Models {
  public sealed class MediaItemsM : ObservableObject {
    private readonly Core _core;
    private readonly SegmentsM _segmentsM;
    private readonly ViewersM _viewersM;

    private bool _isEditModeOn;
    private MediaItemM _current;

    public MediaItemsDataAdapter DataAdapter { get; set; }
    public static HashSet<MediaItemM> ThumbIgnoreCache { get; } = new();
    public HashSet<MediaItemM> ModifiedItems { get; } = new();
    public Dictionary<MediaItemM, ObservableCollection<ITreeItem>> MediaItemVideoClips { get; } = new();
    public MediaItemM Current { get => _current; set { _current = value; OnPropertyChanged(); } }
    public int MediaItemsCount => DataAdapter.All.Count;
    public int ModifiedItemsCount => ModifiedItems.Count;
    public bool IsEditModeOn { get => _isEditModeOn; set { _isEditModeOn = value; OnPropertyChanged(); } }

    public event EventHandler<ObjectEventArgs<MediaItemM>> MediaItemDeletedEventHandler = delegate { };
    public event EventHandler<ObjectEventArgs<List<MediaItemM>>> MediaItemsDeletedEventHandler = delegate { };
    public event EventHandler<ObjectEventArgs<MediaItemM[]>> MediaItemsOrientationChangedEventHandler = delegate { };
    public event EventHandler MetadataChangedEventHandler = delegate { };
    public Action<MediaItemMetadata, bool> ReadMetadata { get; set; }
    public Func<MediaItemM, bool> WriteMetadata { get; set; }

    public RelayCommand<object> CompressCommand { get; }
    public RelayCommand<object> DeleteCommand { get; }
    public RelayCommand<object> RotateCommand { get; }
    public RelayCommand<object> RenameCommand { get; }
    public RelayCommand<ThumbnailsGridM> ImagesToVideoCommand { get; }
    public RelayCommand<object> EditCommand { get; }
    public RelayCommand<object> SaveEditCommand { get; }
    public RelayCommand<ThumbnailsGridM> SelectNotModifiedCommand { get; }
    public RelayCommand<object> CancelEditCommand { get; }
    public RelayCommand<object> CommentCommand { get; }
    public RelayCommand<object> ResizeImagesCommand { get; }
    public RelayCommand<object> ReloadMetadataCommand { get; }
    public RelayCommand<object> AddGeoNamesFromFilesCommand { get; }
    public RelayCommand<FolderM> ReloadMetadataInFolderCommand { get; }
    public RelayCommand<object> RebuildThumbnailsCommand { get; }

    public MediaItemsM(Core core, SegmentsM segmentsM, ViewersM viewersM) {
      _core = core;
      _segmentsM = segmentsM;
      _viewersM = viewersM;

      CompressCommand = new(Compress, () => GetActive().Any());
      DeleteCommand = new(Delete, () => GetActive().Any());
      RotateCommand = new(Rotate, () => GetActive().Any());
      RenameCommand = new(Rename, () => Current != null);

      ResizeImagesCommand = new(
        () => Core.DialogHostShow(new ResizeImagesDialogM(_core.ThumbnailsGridsM.Current.GetSelectedOrAll())),
        () => _core.ThumbnailsGridsM.Current?.FilteredItems.Count > 0);

      EditCommand = new(
        () => IsEditModeOn = true,
        () => !IsEditModeOn);

      SaveEditCommand = new(SaveEdit, () => IsEditModeOn && ModifiedItems.Count > 0);
      CancelEditCommand = new(CancelEdit, () => IsEditModeOn);
      CommentCommand = new(Comment, () => Current != null);

      ReloadMetadataCommand = new(
        () => ReloadMetadata(_core.ThumbnailsGridsM.Current.GetSelectedOrAll()),
        () => _core.ThumbnailsGridsM.Current?.FilteredItems.Count > 0);

      AddGeoNamesFromFilesCommand = new(
        () => AddGeoNamesFromFiles(Core.Settings.GeoNamesUserName),
        () => _core.ThumbnailsGridsM.Current?.FilteredItems.Count(x => x.IsSelected) > 0);

      ReloadMetadataInFolderCommand = new(
        x => ReloadMetadata(x.GetMediaItems(Keyboard.IsShiftOn()), true),
        x => x != null);

      RebuildThumbnailsCommand = new(
        x => RebuildThumbnails(x, Keyboard.IsShiftOn()),
        x => x is FolderM || _core.ThumbnailsGridsM.Current?.FilteredItems.Count > 0);

      ImagesToVideoCommand = new(
        ImagesToVideo,
        (grid) => grid?.FilteredItems.Count(x => x.IsSelected && x.MediaType == MediaType.Image) > 1);

      SelectNotModifiedCommand = new(
        (grid) => grid.Selected.Set(grid.FilteredItems.Except(ModifiedItems).ToArray()),
        (grid) => grid?.FilteredItems.Count > 0);
    }

    private void ImagesToVideo(ThumbnailsGridM grid) {
      Core.DialogHostShow(new ImagesToVideoDialogM(grid.FilteredItems.Where(x => x.IsSelected && x.MediaType == MediaType.Image),
        (folder, fileName) => {
          var mi = AddNew(folder, fileName);
          var mim = new MediaItemMetadata(mi);
          ReadMetadata(mim, false);
          mi.SetThumbSize();
          grid.LoadedItems.Add(mi);
          grid.SoftLoad(grid.LoadedItems, true, true);
        })
      );
    }

    /// <summary>
    /// Sets Current to one after or one before selection
    /// </summary>
    public void SetNewCurrent(List<MediaItemM> items, List<MediaItemM> selected) {
      if (items == null || selected == null || selected.Count == 0)
        Current = null;

      var index = items.IndexOf(selected[^1]) + 1;
      if (index == items.Count)
        index = items.IndexOf(selected[0]) - 1;

      Current = index >= 0
        ? items[index]
        : null;
    }

    public IEnumerable<MediaItemM> GetMediaItems(PersonM person) =>
      DataAdapter.All.Where(mi =>
          mi.People?.Contains(person) == true ||
          mi.Segments?.Any(s => s.Person == person) == true)
        .OrderBy(mi => mi.FileName);

    public IEnumerable<MediaItemM> GetMediaItems(KeywordM keyword, bool recursive) {
      var keywords = new List<KeywordM> { keyword };
      if (recursive) Tree.GetThisAndItemsRecursive(keyword, ref keywords);
      var set = new HashSet<KeywordM>(keywords);

      return DataAdapter.All
        .Where(mi => mi.Keywords?.Any(k => set.Contains(k)) == true
          || mi.Segments?.Any(s => s.Keywords?.Any(k => set.Contains(k)) == true) == true);
    }

    public IEnumerable<MediaItemM> GetMediaItems(GeoNameM geoName, bool recursive) {
      var geoNames = new List<GeoNameM> { geoName };
      if (recursive) Tree.GetThisAndItemsRecursive(geoName, ref geoNames);
      var set = new HashSet<GeoNameM>(geoNames);

      return DataAdapter.All.Where(mi => set.Contains(mi.GeoName))
        .OrderBy(x => x.FileName);
    }

    public MediaItemM CopyTo(MediaItemM mi, FolderM folder, string fileName) {
      var copy = new MediaItemM(DataAdapter.GetNextId(), folder, fileName) {
        Width = mi.Width,
        Height = mi.Height,
        Orientation = mi.Orientation,
        Rating = mi.Rating,
        Comment = mi.Comment,
        GeoName = mi.GeoName,
        Lat = mi.Lat,
        Lng = mi.Lng
      };

      if (mi.People != null)
        copy.People = new(mi.People);

      if (mi.Keywords != null)
        copy.Keywords = new (mi.Keywords);

      if (mi.Segments != null) {
        copy.Segments = new();
        foreach (var segment in mi.Segments) {
          var sCopy = _segmentsM.GetCopy(segment);
          sCopy.MediaItem = copy;
          copy.Segments.Add(sCopy);
        }
      }

      copy.Folder.MediaItems.Add(copy);
      DataAdapter.All.Add(copy);
      OnPropertyChanged(nameof(MediaItemsCount));

      return copy;
    }

    public void MoveTo(MediaItemM mi, FolderM folder, string fileName) {
      mi.FileName = fileName;
      mi.Folder.MediaItems.Remove(mi);
      mi.Folder = folder;
      mi.Folder.MediaItems.Add(mi);

      DataAdapter.IsModified = true;
    }

    private void Rename(MediaItemM mi, string newFileName) {
      var oldFilePath = mi.FilePath;
      var oldFilePathCache = mi.FilePathCache;
      mi.FileName = newFileName;
      File.Move(oldFilePath, mi.FilePath);
      File.Move(oldFilePathCache, mi.FilePathCache);
      DataAdapter.IsModified = true;
    }

    private void Compress() =>
      Core.DialogHostShow(
        new CompressDialogM(
          GetActive().Where(x => x.MediaType == MediaType.Image).ToList(),
          Core.Settings.JpegQualityLevel));

    public void Delete(MediaItemM item) {
      if (item == null) return;

      MediaItemDeletedEventHandler(this, new(item));

      item.People = null;
      item.Keywords = null;
      item.GeoName = null;

      // remove item from Folder
      item.Folder.MediaItems.Remove(item);
      //item.Folder = null;

      // remove from DB
      DataAdapter.All.Remove(item);

      OnPropertyChanged(nameof(MediaItemsCount));

      SetModified(item, false);

      // set MediaItems table as modified
      DataAdapter.IsModified = true;
    }

    public void Delete(List<MediaItemM> items) {
      if (items.Count == 0) return;

      foreach (var mi in items)
        Delete(mi);

      MediaItemsDeletedEventHandler(this, new(items));
    }

    private void Delete() {
      var items = GetActive().ToList();
      var count = items.Count;

      if (Core.DialogHostShow(new MessageDialog(
        "Delete Confirmation",
        $"Do you really want to delete {count} item{(count > 1 ? "s" : string.Empty)}?",
        Res.IconQuestion,
        true)) != 1) return;

      var currentThumbsGrid = _core.ThumbnailsGridsM.Current;
      SetNewCurrent(
        currentThumbsGrid != null
          ? currentThumbsGrid.FilteredItems
          : _core.MediaViewerM.MediaItems,
        items);
      DeleteFromDbAndDrive(items);
    }

    private void DeleteFromDbAndDrive(List<MediaItemM> items) {
      if (items.Count == 0) return;

      var files = new List<string>();
      var cache = new List<string>();

      foreach (var mi in items) {
        files.Add(mi.FilePath);
        cache.Add(mi.FilePathCache);
        Delete(mi);
      }

      Core.FileOperationDelete(files, true, false);
      cache.ForEach(File.Delete);

      MediaItemsDeletedEventHandler(this, new(items));
    }

    private void SetModified(MediaItemM mi, bool value) {
      if (value) {
        ModifiedItems.Add(mi);
        DataAdapter.IsModified = true;
      }
      else
        ModifiedItems.Remove(mi);

      OnPropertyChanged(nameof(ModifiedItemsCount));
    }

    /// <summary>
    /// Copy or Move MediaItems (Files, Cache and DB)
    /// </summary>
    /// <param name="mode"></param>
    /// <param name="items"></param>
    /// <param name="destFolder"></param>
    public void CopyMove(FileOperationMode mode, List<MediaItemM> items, FolderM destFolder) {
      var fop = new FileOperationDialogM(mode, false);
      fop.RunTask = Task.Run(() => {
        fop.LoadCts = new();
        var token = fop.LoadCts.Token;

        try {
          CopyMove(mode, items, destFolder, fop.Progress, token);
        }
        catch (Exception ex) {
          Tasks.RunOnUiThread(() => Core.DialogHostShow(new ErrorDialogM(ex)));
        }
      }).ContinueWith(_ => Tasks.RunOnUiThread(() => fop.Result = 1));

      _ = Core.DialogHostShow(fop);

      if (mode == FileOperationMode.Move) {
        SetNewCurrent(_core.ThumbnailsGridsM.Current.FilteredItems, items);
        _core.ThumbnailsGridsM.Current.Remove(items, true);
      }
    }

    private void CopyMove(FileOperationMode mode, List<MediaItemM> items, FolderM destFolder, IProgress<object[]> progress, CancellationToken token) {
      var count = items.Count;
      var done = 0;
      var replaced = new List<MediaItemM>();

      foreach (var mi in items) {
        if (token.IsCancellationRequested)
          break;

        progress.Report(new object[]
          {Convert.ToInt32((double) done / count * 100), mi.Folder.FullPath, destFolder.FullPath, mi.FileName});

        var miNewFileName = mi.FileName;
        var destFilePath = IOExtensions.PathCombine(destFolder.FullPath, mi.FileName);

        // if the file with the same name exists in the destination
        // show dialog with options to Rename, Replace or Skip the file
        if (File.Exists(destFilePath)) {
          var result = FileOperationCollisionDialogM.Show(mi.FilePath, destFilePath, ref miNewFileName);

          if (result == CollisionResult.Skip) {
            Tasks.RunOnUiThread(() => _core.ThumbnailsGridsM.Current.Selected.Set(mi, false));
            continue;
          }

          if (result == CollisionResult.Replace)
            replaced.Add(mi);
        }

        switch (mode) {
          case FileOperationMode.Copy:
            // create object copy
            var miCopy = CopyTo(mi, destFolder, miNewFileName);
            // copy MediaItem and cache on file system
            Directory.CreateDirectory(Path.GetDirectoryName(miCopy.FilePathCache) ?? throw new ArgumentNullException());
            File.Copy(mi.FilePath, miCopy.FilePath, true);
            File.Copy(mi.FilePathCache, miCopy.FilePathCache, true);

            if (mi.Segments != null)
              for (int i = 0; i < mi.Segments.Count; i++)
                File.Copy(mi.Segments[i].FilePathCache, miCopy.Segments[i].FilePathCache, true);
            
            break;

          case FileOperationMode.Move:
            var srcFilePath = mi.FilePath;
            var srcFilePathCache = mi.FilePathCache;
            var srcDirPathCache = Path.GetDirectoryName(mi.FilePathCache) ?? throw new ArgumentNullException();

            // DB
            MoveTo(mi, destFolder, miNewFileName);

            // File System
            File.Delete(mi.FilePath);
            File.Move(srcFilePath, mi.FilePath);

            // Cache
            Directory.CreateDirectory(Path.GetDirectoryName(mi.FilePathCache) ?? throw new ArgumentNullException());
            // Thumbnail
            File.Delete(mi.FilePathCache);
            if (File.Exists(srcFilePathCache))
              File.Move(srcFilePathCache, mi.FilePathCache);
            // Segments
            foreach (var segment in mi.Segments ?? Enumerable.Empty<SegmentM>()) {
              File.Delete(segment.FilePathCache);
              var srcSegmentPath = Path.Combine(srcDirPathCache, $"segment_{segment.GetHashCode()}.jpg");
              if (File.Exists(srcSegmentPath))
                File.Move(srcSegmentPath, segment.FilePathCache);
            }

            break;
        }

        done++;
      }

      Delete(replaced);
    }

    public MediaItemM AddNew(FolderM folder, string fileName) {
      var mi = new MediaItemM(DataAdapter.GetNextId(), folder, fileName);
      DataAdapter.All.Add(mi);
      OnPropertyChanged(nameof(MediaItemsCount));
      folder.MediaItems.Add(mi);

      return mi;
    }

    public void UpdateInfoBoxWithPerson(PersonM person) {
      foreach (var mi in DataAdapter.All
                 .Where(mi => mi.InfoBoxPeople != null && mi.People?.Contains(person) == true))
        mi.SetInfoBox();
    }

    public void UpdateInfoBoxWithKeyword(KeywordM keyword) {
      foreach (var mi in DataAdapter.All
                 .Where(mi => mi.InfoBoxKeywords != null && mi.Keywords?.Contains(keyword) == true))
        mi.SetInfoBox();
    }

    public void RemovePersonFromMediaItems(PersonM person) {
      foreach (var mi in DataAdapter.All.Where(mi => mi.People?.Contains(person) == true)) {
        mi.People = ListExtensions.Toggle(mi.People, person, true);
        DataAdapter.IsModified = true;
      }
    }

    public void RemoveKeywordFromMediaItems(KeywordM keyword) {
      foreach (var mi in DataAdapter.All.Where(mi => mi.Keywords?.Contains(keyword) == true)) {
        mi.Keywords = KeywordsM.Toggle(mi.Keywords, keyword);
        DataAdapter.IsModified = true;
      }
    }

    public bool TryWriteMetadata(MediaItemM mediaItem) {
      if (mediaItem.IsOnlyInDb) return true;
      try {
        return WriteMetadata(mediaItem) ? true : throw new("Error writing metadata");
      }
      catch (Exception ex) {
        Log.Error(ex, $"Metadata will be saved just in Database. {mediaItem.FilePath}");
        // set MediaItem as IsOnlyInDb to not save metadata to file, but keep them just in DB
        mediaItem.IsOnlyInDb = true;
        return false;
      }
    }

    public void AddGeoNamesFromFiles(string geoNamesUserName) {
      if (!GeoNamesM.IsGeoNamesUserNameInSettings(geoNamesUserName)) return;

      var progress = new ProgressBarDialog("Adding GeoNames ...", Res.IconLocationCheckin, true, 1);
      progress.AddEvents(
        _core.ThumbnailsGridsM.Current.FilteredItems.Where(x => x.IsSelected).ToArray(),
        null,
        // action
        async mi => {
          if (mi.Lat == null || mi.Lng == null) {
            var mim = new MediaItemMetadata(mi);
            ReadMetadata(mim, true);
            if (mim.Success)
              await Tasks.RunOnUiThread(() => mim.FindRefs(_core));
          }
          if (mi.Lat == null || mi.Lng == null) return;

          var lastGeoName = _core.GeoNamesM.InsertGeoNameHierarchy((double)mi.Lat, (double)mi.Lng, geoNamesUserName);
          if (lastGeoName == null) return;

          mi.GeoName = lastGeoName;
          TryWriteMetadata(mi);
          await Tasks.RunOnUiThread(() => {
            mi.SetInfoBox();
            DataAdapter.IsModified = true;
          });
        },
        mi => mi.FilePath,
        // onCompleted
        delegate {
          Current?.GeoName?.OnPropertyChanged(nameof(Current.GeoName.FullName));
        });

      progress.Start();
      Core.DialogHostShow(progress);
    }

    public void RebuildThumbnails(object source, bool recursive) {
      var mediaItems = source switch {
        FolderM folder => folder.GetMediaItems(recursive),
        List<MediaItemM> items => items,
        _ => _core.ThumbnailsGridsM.Current.GetSelectedOrAll(),
      };

      foreach (var mi in mediaItems) {
        mi.SetThumbSize(true);
        ThumbIgnoreCache.Add(mi);
        File.Delete(mi.FilePathCache);
      }

      _core.ThumbnailsGridsM.Current.ReWrapItems = true;
    }

    public void SetOrientation(MediaItemM[] mediaItems, MediaOrientation orientation) {
      var progress = new ProgressBarDialog("Changing orientation ...", Res.IconImage, true, Environment.ProcessorCount);
      progress.AddEvents(
        mediaItems,
        null,
        // action
        mi => {
          var newOrientation = mi.RotationAngle;

          if (mi.MediaType == MediaType.Image) {
            switch (orientation) {
              case MediaOrientation.Rotate90: newOrientation += 90; break;
              case MediaOrientation.Rotate180: newOrientation += 180; break;
              case MediaOrientation.Rotate270: newOrientation += 270; break;
            }
          }
          else if (mi.MediaType == MediaType.Video) {
            // images have switched 90 and 270 angles and all application is made with this in mind
            // so I switched orientation just for video
            switch (orientation) {
              case MediaOrientation.Rotate90: newOrientation += 270; break;
              case MediaOrientation.Rotate180: newOrientation += 180; break;
              case MediaOrientation.Rotate270: newOrientation += 90; break;
            }
          }

          if (newOrientation >= 360) newOrientation -= 360;

          switch (newOrientation) {
            case 0: mi.Orientation = (int)MediaOrientation.Normal; break;
            case 90: mi.Orientation = (int)MediaOrientation.Rotate90; break;
            case 180: mi.Orientation = (int)MediaOrientation.Rotate180; break;
            case 270: mi.Orientation = (int)MediaOrientation.Rotate270; break;
          }

          TryWriteMetadata(mi);
          mi.SetThumbSize(true);
          mi.OnPropertyChanged(nameof(mi.FilePathCache));
        },
        mi => mi.FilePath,
        // onCompleted
        (_, _) => MediaItemsOrientationChangedEventHandler(this, new(mediaItems)));

      progress.Start();
      Core.DialogHostShow(progress);
    }

    public void SaveEdit() {
      var progress = new ProgressBarDialog("Saving metadata ...", Res.IconImage, true, Environment.ProcessorCount);
      progress.AddEvents(
        ModifiedItems.ToArray(),
        null,
        // action
        async mi => {
          TryWriteMetadata(mi);
          await Tasks.RunOnUiThread(() => SetModified(mi, false));
        },
        mi => mi.FilePath,
        // onCompleted
        (_, e) => {
          if (e.Cancelled)
            CancelEdit();
          else
            IsEditModeOn = false;

          _core.StatusPanelM.OnPropertyChanged(nameof(_core.StatusPanelM.FileSize));
        });

      progress.Start();
      Core.DialogHostShow(progress);
    }

    public void CancelEdit() {
      var progress = new ProgressBarDialog("Reloading metadata ...", Res.IconImage, false, Environment.ProcessorCount);
      progress.AddEvents(
        ModifiedItems.ToArray(),
        null,
        // action
        async mi => {
          var mim = new MediaItemMetadata(mi);
          ReadMetadata(mim, false);

          await Tasks.RunOnUiThread(() => {
            if (mim.Success) mim.FindRefs(_core);
            SetModified(mi, false);
            mi.SetInfoBox();
          });
        },
        mi => mi.FilePath,
        // onCompleted
        (_, _) => {
          MetadataChangedEventHandler(this, EventArgs.Empty);
          IsEditModeOn = false;
        });

      progress.Start();
      Core.DialogHostShow(progress);
    }

    public void ReloadMetadata(List<MediaItemM> mediaItems, bool updateInfoBox = false) {
      var progress = new ProgressBarDialog("Reloading metadata ...", Res.IconImage, true, Environment.ProcessorCount);
      progress.AddEvents(
        mediaItems.ToArray(),
        null,
        // action
        async (mi) => {
          var mim = new MediaItemMetadata(mi);
          ReadMetadata(mim, false);
          if (mim.Success)
            await Tasks.RunOnUiThread(() => mim.FindRefs(_core));

          // set info box just for loaded media items
          if (updateInfoBox)
            await Tasks.RunOnUiThread(mi.SetInfoBox);
        },
        mi => mi.FilePath,
        // onCompleted
        (_, _) => MetadataChangedEventHandler(this, EventArgs.Empty));

      progress.Start();
      Core.DialogHostShow(progress);
    }

    private void Rotate() {
      var rotation = Core.DialogHostShow(new RotationDialogM());
      if (rotation == 0) return;

      SetOrientation(GetActive(), (MediaOrientation)rotation);

      if (_core.MediaViewerM.IsVisible)
        _core.MediaViewerM.Current = _core.MediaViewerM.Current;
    }

    public async void Rename() {
      var inputDialog = new InputDialog(
        "Rename",
        "Add a new name.",
        Res.IconNotification,
        Path.GetFileNameWithoutExtension(Current.FileName),
        answer => {
          var newFileName = answer + Path.GetExtension(Current.FileName);

          if (Path.GetInvalidFileNameChars().Any(x => newFileName.IndexOf(x) != -1))
            return "New file name contains invalid character!";

          if (File.Exists(IOExtensions.PathCombine(Current.Folder.FullPath, newFileName)))
            return "New file name already exists!";

          return string.Empty;
        });
        
      if (Core.DialogHostShow(inputDialog) != 1) return;

      try {
        Rename(Current, inputDialog.Answer + Path.GetExtension(Current.FileName));
        _core.ThumbnailsGridsM.Current?.SoftLoad(_core.ThumbnailsGridsM.Current.FilteredItems, true, false);
        OnPropertyChanged(nameof(Current));
      }
      catch (Exception ex) {
        Log.Error(ex);
      }
    }

    public void Comment() {
      var inputDialog = new InputDialog(
        "Comment",
        "Add a comment.",
        Res.IconNotification,
        Current.Comment,
        answer => answer.Length > 256
          ? "Comment is too long!"
          : string.Empty);

      if (Core.DialogHostShow(inputDialog) != 1) return;

      Current.Comment = StringUtils.NormalizeComment(inputDialog.Answer);
      Current.SetInfoBox();
      Current.OnPropertyChanged(nameof(Current.Comment));
      TryWriteMetadata(Current);
      DataAdapter.IsModified = true;
    }

    public MediaItemM[] GetActive() =>
      _core.MainWindowM.IsFullScreen
        ? Current == null
          ? Array.Empty<MediaItemM>()
          : new[] { Current }
        : _core.ThumbnailsGridsM.Current == null
          ? Array.Empty<MediaItemM>()
          : _core.ThumbnailsGridsM.Current.Selected.Items.ToArray();

    public void SetMetadata(object item) {
      var items = GetActive();
      if (items.Length == 0) return;

      var count = 0;

      foreach (var mi in items) {
        var modified = true;

        switch (item) {
          case PersonM p:
            mi.People = ListExtensions.Toggle(mi.People, p, true);
            break;

          case KeywordM k:
            mi.Keywords = KeywordsM.Toggle(mi.Keywords, k);
            break;

          case RatingTreeM r:
            mi.Rating = r.Rating.Value;
            break;

          case GeoNameM g:
            mi.GeoName = g;
            break;

          default:
            modified = false;
            break;
        }

        if (!modified) continue;

        SetModified(mi, true);
        mi.SetInfoBox();
        count++;
      }

      if (count > 0)
        MetadataChangedEventHandler(this, EventArgs.Empty);
    }

    public static bool IsPanoramic(MediaItemM mi) =>
      mi.Orientation is (int)MediaOrientation.Rotate90 or (int)MediaOrientation.Rotate270
        ? mi.Height / (double)mi.Width > 16.0 / 9.0
        : mi.Width / (double)mi.Height > 16.0 / 9.0;
  }
}
