﻿namespace PictureManager.Domain {
  public static class Res {
    public const string IconEmpty = null;
    public const string IconCompare = "IconCompare";
    public const string IconFolder = "IconFolder";
    public const string IconFolderStar = "IconFolderStar";
    public const string IconFolderLock = "IconFolderLock";
    public const string IconFolderPuzzle = "IconFolderPuzzle";
    public const string IconFolderOpen = "IconFolderOpen";
    public const string IconTag = "IconTag";
    public const string IconTagLabel = "IconTagLabel";
    public const string IconPeople = "IconPeople";
    public const string IconPeopleMultiple = "IconPeopleMultiple";
    public const string IconDrive = "IconDrive";
    public const string IconDriveError = "IconDriveError";
    public const string IconCd = "IconCd";
    public const string IconSave = "IconSave";
    public const string IconSettings = "IconSettings";
    public const string IconStar = "IconStar";
    public const string IconSort = "IconSort";
    public const string IconRuler = "IconRuler";
    public const string IconLocationCheckin = "IconLocationCheckin";
    public const string IconEye = "IconEye";
    public const string IconMovieClapper = "IconMovieClapper";
    public const string IconInformation = "IconInformation";
    public const string IconNotification = "IconNotification";
    public const string IconQuestion = "IconQuestion";
    public const string IconBug = "IconBug";
    public const string IconImage = "IconImage";
    public const string IconImageMultiple = "IconImageMultiple";
    public const string IconCalendar = "IconCalendar";
    public const string IconEquals = "IconEquals";
    public const string IconCheckMark = "IconCheckMark";
    public const string IconXCross = "IconXCross";

    public static string CategoryToIconName(Category category) =>
      category switch {
        Category.FavoriteFolders => IconFolderStar,
        Category.Folders => IconFolder,
        Category.Ratings => IconStar,
        Category.People => IconPeopleMultiple,
        Category.FolderKeywords => IconFolder,
        Category.Keywords => IconTagLabel,
        Category.Viewers => IconEye,
        Category.VideoClips => IconMovieClapper,
        _ => IconEmpty
      };
  }
}
