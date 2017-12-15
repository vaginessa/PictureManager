﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using PictureManager.Properties;
using PictureManager.ShellStuff;
using Directory = System.IO.Directory;

namespace PictureManager {
  public class AppCore : IDisposable {
    private ViewModel.BaseTreeViewItem _lastSelectedSource;
    public ObservableCollection<ViewModel.BaseTreeViewItem> FoldersRoot;
    public ObservableCollection<ViewModel.BaseTreeViewItem> KeywordsRoot;
    public ObservableCollection<ViewModel.BaseTreeViewItem> FiltersRoot;
    public ViewModel.Keywords Keywords;
    public ViewModel.People People;
    public ViewModel.FolderKeywords FolderKeywords;
    public ViewModel.Folders Folders;
    public ViewModel.FavoriteFolders FavoriteFolders;
    public ViewModel.Ratings Ratings;
    public ViewModel.Filters Filters;
    public ViewModel.Viewers Viewers;
    public ViewModel.GeoNames GeoNames;
    public ViewModel.SqlQueries SqlQueries;

    public WMain WMain;
    public string[] IncorectChars = { "\\", "/", ":", "*", "?", "\"", "<", ">", "|", ";" };
    public System.Windows.Forms.WebBrowser WbThumbs;
    public ViewModel.AppInfo AppInfo;
    public bool OneFileOnly;
    public bool ViewerOnly = false; //application was run with file path parameter
    public DataModel.PmDataContext Db;
    public ViewModel.MediaItems MediaItems;
    public List<ViewModel.BaseTreeViewTagItem> MarkedTags;
    public BackgroundWorker ThumbsWebWorker;
    public AutoResetEvent ThumbsResetEvent = new AutoResetEvent(false);
    public int ThumbsPageIndex;
    public int ThumbsPerPage = 300;
    public ViewModel.Viewer CurrentViewer;

    private bool _keywordsEditMode;

    public bool KeywordsEditMode {
      get { return _keywordsEditMode; }
      set {
        _keywordsEditMode = value;
        AppInfo.KeywordsEditMode = value;
      }
    }

    public ViewModel.BaseTreeViewItem LastSelectedSource {
      get { return _lastSelectedSource; }
      set {
        if (_lastSelectedSource == value) return;
        if (_lastSelectedSource != null)
          _lastSelectedSource.IsSelected = false;
        _lastSelectedSource = value;
      }
    }

    public void Dispose() {
      Dispose(true);
      GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing) {
      if (!disposing) return;

      if (Db != null) {
        Db.Dispose();
        Db = null;
      }

      if (ThumbsResetEvent != null) {
        ThumbsResetEvent.Dispose();
        ThumbsResetEvent = null;
      }
    }

    public void InitBase() {
      AppInfo = new ViewModel.AppInfo();
      MediaItems = new ViewModel.MediaItems();
      MarkedTags = new List<ViewModel.BaseTreeViewTagItem>();

      Db = new DataModel.PmDataContext("Data Source = data.db");
      Db.Load();
    }

    public void Init() {
      People = new ViewModel.People {CanHaveGroups = true, CanModifyItems = true};
      Keywords = new ViewModel.Keywords {CanHaveGroups = true, CanHaveSubItems = true, CanModifyItems = true};
      FolderKeywords = new ViewModel.FolderKeywords();
      Folders = new ViewModel.Folders();
      FavoriteFolders = new ViewModel.FavoriteFolders();
      Ratings = new ViewModel.Ratings();
      Filters = new ViewModel.Filters();
      Viewers = new ViewModel.Viewers {CanModifyItems = true};
      GeoNames = new ViewModel.GeoNames();
      SqlQueries = new ViewModel.SqlQueries {CanHaveGroups = true, CanModifyItems = true};

      People.Load();
      Keywords.Load();
      FolderKeywords.Load();
      Folders.AddDrives();
      FavoriteFolders.Load();
      Ratings.Load();
      Filters.Load();
      Viewers.Load();
      GeoNames.Load();
      SqlQueries.Load();

      FoldersRoot = new ObservableCollection<ViewModel.BaseTreeViewItem> {FavoriteFolders, Folders};
      KeywordsRoot = new ObservableCollection<ViewModel.BaseTreeViewItem> {Ratings, People, FolderKeywords, Keywords, GeoNames};
      FiltersRoot = new ObservableCollection<ViewModel.BaseTreeViewItem> {Filters, Viewers, SqlQueries};
    }

    public void UpdateStatusBarInfo() {
      var flag = AppInfo.AppMode == AppModes.KeywordsEdit;
      var iTotal = MediaItems.Items.Count;
      var iSelected = MediaItems.Items.Count(x => x.IsSelected);
      var iModifed = flag ? MediaItems.Items.Count(p => p.IsModifed) : 0;
      AppInfo.ViewBaseInfo = $"{iTotal} object(s) / {iSelected} selected{(flag ? $" / {iModifed} modifed" : string.Empty)}";
      AppInfo.CurrentPictureFilePath = MediaItems.Current == null ? string.Empty : MediaItems.Current.FilePath;
    }

    public void TreeView_FoldersStackPanel_PreviewMouseUp(object item, MouseButton mouseButton, bool recursive) {
      if (mouseButton == MouseButton.Left) {
        switch (item.GetType().Name) {
          case nameof(ViewModel.Folders):
          case nameof(ViewModel.FavoriteFolders): {
            ((ViewModel.BaseTreeViewItem)item).IsSelected = false;
            break;
          }
          case nameof(ViewModel.Folder): {
            var folder = (ViewModel.Folder)item;
            if (!folder.IsAccessible) {
              folder.IsSelected = false;
              return;
            }

            folder.IsSelected = true;
            LastSelectedSource = folder;

            if (ThumbsWebWorker != null && ThumbsWebWorker.IsBusy) {
              ThumbsWebWorker.CancelAsync();
              ThumbsResetEvent.WaitOne();
            }

            MediaItems.LoadByFolder(folder.FullPath, recursive);
            InitThumbsPagesControl();
            break;
          }
          case nameof(ViewModel.FavoriteFolder): {
            var folder = Folders.ExpandTo(((ViewModel.FavoriteFolder)item).FullPath);
            if (folder != null) {
              var visibleTreeIndex = 0;
              Folders.GetVisibleTreeIndexFor(Folders.Items, folder, ref visibleTreeIndex);
              var offset = (FavoriteFolders.Items.Count + 1 + visibleTreeIndex) * 25;
              WMain.TvFoldersScrollViewer.ScrollToVerticalOffset(offset);
            }
            break;
          }
        }
      }
    }

    public void TreeView_FiltersStackPanel_PreviewMouseUp(object item, MouseButton mouseButton) {
      if (KeywordsEditMode) return;
      if (mouseButton != MouseButton.Left) return;

      var sqlQuery = item as ViewModel.SqlQuery;
      if (sqlQuery != null) {
        TreeView_KeywordsStackPanel_PreviewMouseUp(item, mouseButton, false);
        return;
      }

      var filter = item as ViewModel.Filter;
      if (filter == null) return;
      filter.IsSelected = true;
      LastSelectedSource = filter;

      if (ThumbsWebWorker != null && ThumbsWebWorker.IsBusy) {
        ThumbsWebWorker.CancelAsync();
        ThumbsResetEvent.WaitOne();
      }

      MediaItems.LoadByFilter(filter);
      InitThumbsPagesControl();
    }

    public void TreeView_KeywordsStackPanel_PreviewMouseUp(object item, MouseButton mouseButton, bool recursive) {
      if (item is ViewModel.Keywords || item is ViewModel.People || item is ViewModel.FolderKeywords || item is ViewModel.Ratings || item is ViewModel.CategoryGroup || item is ViewModel.GeoNames || item is ViewModel.SqlQueries) return;

      switch (mouseButton) {
        case MouseButton.Left: {
          if (KeywordsEditMode) {
            var fk = item as ViewModel.FolderKeyword;
            if (fk != null) {
              fk.IsSelected = false;
              return;
            }

            var bti = item as ViewModel.BaseTreeViewTagItem;
            if (bti != null) {
                bti.IsMarked = !bti.IsMarked;
              if (bti.IsMarked)
                MarkedTags.Add(bti);
              else
                MarkedTags.Remove(bti);
            }

            MediaItems.EditMetadata(item);

            MarkUsedKeywordsAndPeople();
            UpdateStatusBarInfo();
          } else {
            //not KeywordsEditMode
            var baseTagItem = (ViewModel.BaseTreeViewTagItem) item;

            if (baseTagItem is ViewModel.Rating || (!recursive && baseTagItem is ViewModel.Person)) {
              if (LastSelectedSource != null) LastSelectedSource.IsSelected = true;
              baseTagItem.BgBrush = baseTagItem.BgBrush == BgBrushes.Chosen ? BgBrushes.Default : BgBrushes.Chosen;
              baseTagItem.IsSelected = false;
            }
            else {
              baseTagItem.IsSelected = true;
              LastSelectedSource = baseTagItem;
            }

            if (ThumbsWebWorker != null && ThumbsWebWorker.IsBusy) {
              ThumbsWebWorker.CancelAsync();
              ThumbsResetEvent.WaitOne();
            }

            var folder = LastSelectedSource as ViewModel.Folder;
            if (folder != null) {
              MediaItems.LoadByFolder(folder.FullPath, false);
              InitThumbsPagesControl();
              return;
            }

            MediaItems.LoadByTag((ViewModel.BaseTreeViewTagItem) LastSelectedSource, recursive);
            InitThumbsPagesControl();
          }
          break;
        }
      }
    }

    public void MarkUsedKeywordsAndPeople() {
      //can by Person, Keyword, FolderKeyword, Rating or GeoName
      foreach (var item in MarkedTags) {
        item.IsMarked = false;
        item.PicCount = 0;
      }
      MarkedTags.Clear();

      var mediaItems = MediaItems.GetSelectedOrAll();
      foreach (var mi in mediaItems) {

        foreach (var person in mi.People.Where(person => !person.IsMarked)) {
          person.IsMarked = true;
          MarkedTags.Add(person);
        }

        foreach (var keyword in mi.Keywords) {
          var k = keyword;
          do {
            if (k.IsMarked) break;
            k.IsMarked = true;
            MarkedTags.Add(k);
            k = k.Parent as ViewModel.Keyword;
          } while (k != null);
        }

        var folderKeyword = FolderKeywords.GetFolderKeywordByDirId(FolderKeywords.Items, mi.DirId);
        if (folderKeyword != null && !folderKeyword.IsMarked) {
          var fk = folderKeyword;
          do {
            if (fk.IsMarked) break;
            fk.IsMarked = true;
            MarkedTags.Add(fk);
            fk = (ViewModel.FolderKeyword) fk.Parent;
          } while (fk != null);
        }

        var geoName = GeoNames.AllGeoNames.SingleOrDefault(x => x.GeoNameId == mi.GeoNameId);
        if (geoName != null && !geoName.IsMarked) {
          var gn = geoName;
          do {
            if (gn.IsMarked) break;
            gn.IsMarked = true;
            MarkedTags.Add(gn);
            gn = (ViewModel.GeoName) gn.Parent;
          } while (gn != null);
        }
      }

      foreach (var rating in mediaItems.Select(p => p.Rating).Distinct().Select(r => Ratings.GetRatingByValue(r))) {
        rating.IsMarked = true;
        MarkedTags.Add(rating);
      }

      foreach (var item in MarkedTags) {
        switch (item.GetType().Name) {
          case nameof(ViewModel.Person): {
            var pesron = (ViewModel.Person) item;
            pesron.PicCount = mediaItems.Count(p => p.People.Contains(pesron));
            break;
          }
          case nameof(ViewModel.Keyword): {
            var keyword = (ViewModel.Keyword) item;
            keyword.PicCount = mediaItems.Count(p => p.Keywords.Any(k => k.FullPath.StartsWith(keyword.FullPath)));
            break;
          }
          case nameof(ViewModel.FolderKeyword): {
            var folderKeyword = (ViewModel.FolderKeyword) item;
            folderKeyword.PicCount =
              mediaItems.Count(p => p.FolderKeyword != null && p.FolderKeyword.FullPath.StartsWith(folderKeyword.FullPath));
            break;
          }
          case nameof(ViewModel.Rating): {
            var rating = (ViewModel.Rating) item;
            rating.PicCount = mediaItems.Count(p => p.Rating == rating.Value);
            break;
          }
          case nameof(ViewModel.GeoName): {
              //TODO C# how to count files in subdirectories
            var geoName = (ViewModel.GeoName) item;

            var geoNames = new List<ViewModel.GeoName>();
            geoName.GetThisAndSubGeoNames(ref geoNames);
            geoName.PicCount = mediaItems.Count(x => geoNames.Select(gn => (int?) gn.GeoNameId).Contains(x.GeoNameId));



            /*  var picCount = mediaItems.Count(x => x.GeoNameId == geoName.GeoNameId);
            if (picCount != 0) geoName.PicCount = picCount;
            var parent = geoName.Parent as ViewModel.BaseTreeViewTagItem;
            if (parent != null) parent.PicCount += geoName.PicCount;*/
            break;
          }
        }
      }

      foreach (var pg in People.Items.Where(x => x is ViewModel.CategoryGroup).Cast<ViewModel.CategoryGroup>()) {
        pg.PicCount = pg.Items.Cast<ViewModel.Person>().Sum(x => x.PicCount);
        pg.IsMarked = pg.PicCount > 0;
      }

      foreach (var kg in Keywords.Items.Where(x => x is ViewModel.CategoryGroup).Cast<ViewModel.CategoryGroup>()) {
        kg.PicCount = kg.Items.Cast<ViewModel.Keyword>().Sum(x => x.PicCount);
        kg.IsMarked = kg.PicCount > 0;
      }
    }

    public void InitThumbsPagesControl() {
      WMain.CmbThumbPage.Visibility = MediaItems.Items.Count > ThumbsPerPage ? Visibility.Visible : Visibility.Collapsed;
      WMain.CmbThumbPage.Items.Clear();
      var iPageCount = MediaItems.Items.Count / ThumbsPerPage;
      if (iPageCount == 0 || MediaItems.Items.Count > ThumbsPerPage) iPageCount++;
      for (var i = 0; i < iPageCount; i++) {
        WMain.CmbThumbPage.Items.Add($"Page {i + 1}");
      }
      //this will start ACore.CreateThumbnailsWebPage()
      WMain.CmbThumbPage.SelectedIndex = 0;
    }

    public void CreateThumbnailsWebPage() {
      var doc = WbThumbs.Document;
      var thumbs = doc?.GetElementById("thumbnails");
      if (thumbs == null) return;

      thumbs.InnerHtml = string.Empty;
      doc.Window?.ScrollTo(0, 0);

      WMain.StatusProgressBar.Value = 0;
      WMain.StatusProgressBar.Maximum = 100;

      ThumbsWebWorker = new BackgroundWorker {WorkerReportsProgress = true, WorkerSupportsCancellation = true};

      ThumbsWebWorker.ProgressChanged += delegate(object sender, ProgressChangedEventArgs e) {
        if (((BackgroundWorker) sender).CancellationPending || e.UserState == null) return;

        var mi = MediaItems.Items[(int) e.UserState];
        var thumb = doc.CreateElement("div");
        var keywords = doc.CreateElement("div");
        var bmi = doc.CreateElement(mi.MediaType == MediaTypes.Image ? "img" : "video");
        //var bmi = doc.CreateElement("img");

        if (thumb == null || keywords == null || bmi == null) return;

        keywords.SetAttribute("className", "keywords");
        keywords.InnerHtml = mi.GetKeywordsAsString(false);

        bmi.SetAttribute("src", mi.MediaType == MediaTypes.Image ? mi.FilePathCache : mi.FilePath);

        if (mi.MediaType == MediaTypes.Video) {
          //bmi.SetAttribute("autoplay", "true");
          bmi.SetAttribute("loop", "true");
          //bmi.SetAttribute("muted", "true");
          bmi.SetAttribute("poster", mi.FilePathCache);
          bmi.SetAttribute("controls", "true");
          //bmi.SetAttribute(mi.Width > mi.Height ? "width" : "height", Settings.Default.ThumbnailSize.ToString());
          //TODO get dimensions for video's
          bmi.SetAttribute("width", Settings.Default.ThumbnailSize.ToString());
        }

        //bmi.SetAttribute("src", mi.FilePathCache);
        thumb.SetAttribute("className", "thumbBox");
        thumb.SetAttribute("id", mi.Index.ToString());
        thumb.AppendChild(keywords);
        thumb.AppendChild(bmi);
        thumbs.AppendChild(thumb);

        WMain.StatusProgressBar.Value = e.ProgressPercentage;
      };

      ThumbsWebWorker.DoWork += delegate(object sender, DoWorkEventArgs e) {
        var worker = (BackgroundWorker) sender;
        var count = MediaItems.Items.Count;
        var iFrom = ThumbsPageIndex == 0 ? 0 : ThumbsPageIndex * ThumbsPerPage;
        var iTo = count > iFrom + ThumbsPerPage ? iFrom + ThumbsPerPage : count;
        var done = 0;
        e.Result = e.Argument;

        for (var i = iFrom; i < iTo; i++) {
          if (worker.CancellationPending) {
            e.Cancel = true;
            ThumbsResetEvent.Set();
            break;
          }
          var mi = MediaItems.Items[i];
          var thumbPath = mi.FilePathCache;
          var flag = File.Exists(thumbPath);
          if (!flag) CreateThumbnail(mi.FilePath, thumbPath);

          if (mi.Data == null) {
            mi.SaveMediaItemInToDb(false, true, (List<DataModel.BaseTable>[]) e.Argument);
            Application.Current.Properties[nameof(AppProps.SubmitChanges)] = true;
          }

          done++;
          worker.ReportProgress(Convert.ToInt32(((double) done/(iTo - iFrom))*100), mi.Index);
        }
      };

      ThumbsWebWorker.RunWorkerCompleted += delegate (object sender, RunWorkerCompletedEventArgs e) {
        if (((BackgroundWorker) sender).CancellationPending) return;
        if ((bool) Application.Current.Properties[nameof(AppProps.SubmitChanges)])
          Db.SubmitChanges((List<DataModel.BaseTable>[]) e.Result);
        MediaItems.ScrollToCurrent();
        if (MediaItems.Current != null) {
          MediaItems.Current.IsSelected = false;
          MediaItems.Current.IsSelected = true;
        }
        MarkUsedKeywordsAndPeople();
      };

      Application.Current.Properties[nameof(AppProps.SubmitChanges)] = false;
      ThumbsWebWorker.RunWorkerAsync(Db.GetInsertUpdateDeleteLists());
    }

    public bool FileOperation(FileOperations mode, bool recycle) {
      return FileOperation(mode, null, null, null, recycle);
    }

    public bool FileOperation(FileOperations mode, string from, bool recycle) {
      return FileOperation(mode, from, null, null, recycle);
    }

    public bool FileOperation(FileOperations mode, string from, string to, string newName) {
      return FileOperation(mode, from, to, newName, true);
    }

    /// <summary>
    /// Operates only with selected MediaItems
    /// </summary>
    /// <param name="mode"></param>
    /// <param name="to"></param>
    /// <returns></returns>
    public bool FileOperation(FileOperations mode, string to) {
      return FileOperation(mode, null, to, null, true);
    }

    public bool FileOperation(FileOperations mode, string from, string to, string newName, bool recycle) {
      Application.Current.Properties[nameof(AppProps.FileOperationResult)] = new Dictionary<string, string>();
      //Copy, Move or delete selected MediaItems or folder
      using (FileOperation fo = new FileOperation(new PicFileOperationProgressSink())) {
        var flags = FileOperationFlags.FOF_NOCONFIRMMKDIR | (recycle
          ? FileOperationFlags.FOFX_RECYCLEONDELETE
          : FileOperationFlags.FOF_WANTNUKEWARNING);
        fo.SetOperationFlags(flags);
        if (from == null) { //MediaItems
          foreach (var mi in MediaItems.Items.Where(x => x.IsSelected)) {
            switch (mode) {
              case FileOperations.Copy: { fo.CopyItem(mi.FilePath, to, mi.FileNameWithExt); break; }
              case FileOperations.Move: { fo.MoveItem(mi.FilePath, to, mi.FileNameWithExt); break; }
              case FileOperations.Delete: { fo.DeleteItem(mi.FilePath); break; }
            }
          }
        } else { //Folders
          switch (mode) {
            case FileOperations.Copy: { fo.CopyItem(from, to, newName); break; }
            case FileOperations.Move: { fo.MoveItem(from, to, newName); break; }
            case FileOperations.Delete: { fo.DeleteItem(from); break; }
          }
        }

        fo.PerformOperations();
      }

      var foResult = (Dictionary<string, string>)Application.Current.Properties[nameof(AppProps.FileOperationResult)];
      if (foResult.Count == 0) return false;

      //update DB and thumbnail cache
      using (FileOperation fo = new FileOperation()) {
        fo.SetOperationFlags(FileOperationFlags.FOF_SILENT | FileOperationFlags.FOF_NOCONFIRMATION |
                             FileOperationFlags.FOF_NOERRORUI | FileOperationFlags.FOFX_KEEPNEWERFILE);
        var cachePath = @Settings.Default.CachePath;
        var mItems = Db.MediaItems;
        var dirs = Db.Directories;
        var lists = Db.GetInsertUpdateDeleteLists();

        if (mode == FileOperations.Delete) {
          var itemsToDel = new List<DataModel.MediaItem>();

          if (from == null) {
            //delete by file/s
            foreach (var mi in MediaItems.Items.Where(x => x.IsSelected)) {
              if (File.Exists(mi.FilePath)) continue;
              var cacheFilePath = mi.FilePath.Replace(":\\", cachePath);
              if (!File.Exists(cacheFilePath)) continue;
              fo.DeleteItem(cacheFilePath);
              itemsToDel.Add(mi.Data);
            }
          } else {
            //delete by folder
            foreach (var dir in dirs.Where(x => x.Path.Equals(from) || x.Path.StartsWith(from + "\\"))) {
              foreach (var mi in mItems.Where(x => x.DirectoryId.Equals(dir.Id))) {
                var miFilePath = Path.Combine(dir.Path, mi.FileName);
                if (File.Exists(miFilePath)) continue;
                var cacheFilePath = miFilePath.Replace(":\\", cachePath);
                if (!File.Exists(cacheFilePath)) continue;
                fo.DeleteItem(cacheFilePath);
                itemsToDel.Add(mi);
              }
            }
          }

          foreach (var mi in itemsToDel) {
            foreach(var mik in Db.MediaItemKeywords.Where(x => x.MediaItemId == mi.Id)) {
              Db.DeleteOnSubmit(mik, lists);
            }

            foreach (var mip in Db.MediaItemPeople.Where(x => x.MediaItemId == mi.Id)) {
              Db.DeleteOnSubmit(mip, lists);
            }

            Db.DeleteOnSubmit(mi, lists);
          }
        }

        if (mode == FileOperations.Copy || mode == FileOperations.Move) {
          foreach (var item in foResult) {
            if (MediaItems.SuportedExts.Any(ext => item.Value.EndsWith(ext, StringComparison.OrdinalIgnoreCase))) {
              if (!File.Exists(item.Value)) continue;

              var srcDirId = dirs.SingleOrDefault(x => x.Path.Equals(Path.GetDirectoryName(item.Key)))?.Id;
              if (srcDirId == null) continue;

              var srcPic = mItems.SingleOrDefault(x => x.DirectoryId == srcDirId && x.FileName == Path.GetFileName(item.Key));
              if (srcPic == null) continue;

              //get destination directory or create it if doesn't exists
              var dirPath = Path.GetDirectoryName(item.Value);
              var destDirId = Db.InsertDirecotryInToDb(dirPath);

              #region Copy files

              if (mode == FileOperations.Copy) {
                //duplicate Picture
                var destPicId = Db.GetNextIdFor<DataModel.MediaItem>();

                Db.InsertOnSubmit(new DataModel.MediaItem {
                  Id = destPicId,
                  DirectoryId = destDirId,
                  FileName = Path.GetFileName(item.Value),
                  Rating = srcPic.Rating,
                  Comment = srcPic.Comment,
                  Orientation = srcPic.Orientation
                }, lists);

                //duplicate Picture Keywords
                foreach (var mik in Db.MediaItemKeywords.Where(x => x.MediaItemId == srcPic.Id)) {
                  Db.InsertOnSubmit(new DataModel.MediaItemKeyword {
                    Id = Db.GetNextIdFor<DataModel.MediaItemKeyword>(),
                    KeywordId = mik.KeywordId,
                    MediaItemId = destPicId
                  }, lists);
                }

                //duplicate Picture People
                foreach (var mip in Db.MediaItemPeople.Where(x => x.MediaItemId == srcPic.Id)) {
                  Db.InsertOnSubmit(new DataModel.MediaItemPerson {
                    Id = Db.GetNextIdFor<DataModel.MediaItemPerson>(),
                    PersonId = mip.PersonId,
                    MediaItemId = destPicId
                  }, lists);
                }

                //duplicate thumbnail
                fo.CopyItem(item.Key.Replace(":\\", cachePath), Path.GetDirectoryName(item.Value)?.Replace(":\\", cachePath),
                  Path.GetFileName(item.Value));
              }

              #endregion

              #region Move files
              if (mode == FileOperations.Move) {
                //BUG: if the file already exists in the destination directory, FileOperation returns COPYENGINE_S_USER_IGNORED and source thumbnail file is not deleted
                srcPic.DirectoryId = destDirId;
                srcPic.FileName = Path.GetFileName(item.Value);
                Db.UpdateOnSubmit(srcPic, lists);

                //delete empty directory
                if (mItems.Count(x => x.DirectoryId.Equals(srcDirId)) == 0) {
                  var emptyDir = dirs.SingleOrDefault(x => x.Id.Equals(srcDirId));
                  if (emptyDir != null) {
                    Db.DeleteOnSubmit(emptyDir, lists);
                  }
                }

                //move thumbnail
                fo.MoveItem(item.Key.Replace(":\\", cachePath), Path.GetDirectoryName(item.Value)?.Replace(":\\", cachePath),
                  Path.GetFileName(item.Value));
              }

              #endregion
            } else {
              #region Move directories
              if (mode == FileOperations.Move) {
                //test if it is directory
                if (!Directory.Exists(item.Value)) continue;

                foreach (var dir in dirs.Where(x => x.Path.Equals(item.Key) || x.Path.StartsWith(item.Key + "\\"))) {
                  dir.Path = dir.Path.Replace(item.Key, item.Value);
                  Db.UpdateOnSubmit(dir, lists);
                }

                //move thumbnails
                var destPath = Path.GetDirectoryName(item.Value);
                if (destPath != null)
                  fo.MoveItem(item.Key.Replace(":\\", cachePath), destPath.Replace(":\\", cachePath),
                    item.Value.Substring(destPath.EndsWith("\\") ? destPath.Length : destPath.Length + 1));
              }
              #endregion
            }
          }
        }

        fo.PerformOperations();
        Db.SubmitChanges(lists);
      }

      return true;
    }

    public bool CanViewerSeeThisFile(string filePath) {
      bool ok;
      if (CurrentViewer == null) return true;

      var incFo = CurrentViewer.IncludedFolders.Items.Select(x => x.ToolTip).ToArray();
      var excFo = CurrentViewer.ExcludedFolders.Items.Select(x => x.ToolTip).ToArray();
      var incFi = new string[0];
      var excFi = new string[0];

      if (incFo.Any(x => filePath.StartsWith(x, StringComparison.OrdinalIgnoreCase))) {
        if (excFo.Any(x => filePath.StartsWith(x, StringComparison.OrdinalIgnoreCase))) {
          ok = incFi.Any(x => filePath.Equals(x, StringComparison.OrdinalIgnoreCase));
        } else {
          ok = !excFi.Any(x => filePath.Equals(x, StringComparison.OrdinalIgnoreCase));
        }
      } else {
        ok = incFi.Any(x => filePath.Equals(x, StringComparison.OrdinalIgnoreCase));
      }

      return ok;
    }

    public bool CanViewerSeeThisDirectory(string dirPath) {
      bool ok;
      if (CurrentViewer == null) return true;

      var incFo = CurrentViewer.IncludedFolders.Items.Select(x => x.ToolTip).ToArray();
      var excFo = CurrentViewer.ExcludedFolders.Items.Select(x => x.ToolTip).ToArray();
      var incFi = new string[0];
      var excFi = new string[0];

      if (incFo.Any(x => x.Contains(dirPath)) || incFo.Any(dirPath.Contains)) {
        if (excFo.Any(x => x.Contains(dirPath)) || excFo.Any(dirPath.Contains)) {
          ok = incFi.Any(x => x.StartsWith(dirPath));
        } else {
          ok = !excFi.Any(x => x.StartsWith(dirPath));
        }
      } else {
        ok = incFi.Any(x => x.StartsWith(dirPath));
      }

      return ok;
    }

    public static void CreateThumbnail(string origPath, string newPath) {
      var size = Settings.Default.ThumbnailSize;
      var dir = Path.GetDirectoryName(newPath);
      if (dir == null) return;
      Directory.CreateDirectory(dir);

      var process = new Process {
        StartInfo = new ProcessStartInfo {
          Arguments = $"src|\"{origPath}\" dest|\"{newPath}\" quality|\"{80}\" size|\"{size}\"",
          FileName = "ThumbnailCreator.exe",
          UseShellExecute = false,
          CreateNoWindow = true
        }
      };

      process.Start();
      process.WaitForExit(1000);
    }
  }
}
