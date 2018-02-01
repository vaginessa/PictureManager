﻿namespace PictureManager {
  public enum BgBrushes { Default = 0, Marked = 1, OrThis = 2, AndThis = 3, Hidden = 4 }
  public enum AppModes { Browser = 0, Viewer = 1, KeywordsEdit = 2, ViewerEdit = 3 }
  public enum AppProps { AppCore, SubmitChanges, FileOperationResult, EditedViewer, EditKeywordsFromFolders }
  public enum FileOperations { Copy, Move, Delete }
  public enum Categories { FavoriteFolders = 0, Folders = 1, Ratings = 2, People = 3, FolderKeywords = 4, Keywords = 5, Filters = 6, Viewers = 7, GeoNames = 8, SqlQueries = 9 }
  public enum MediaTypes { Image = 0, Video = 1 }
}