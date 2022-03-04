﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MH.Utils;
using MH.Utils.BaseClasses;
using MH.Utils.Extensions;
using PictureManager.Domain.Interfaces;

namespace PictureManager.Domain.Models {
  public sealed class ThumbnailsGridM : ObservableObject {
    private readonly List<MediaItemM> _selectedItems = new();
    private readonly List<object> _filterAnd = new();
    private readonly List<object> _filterOr = new();
    private readonly List<object> _filterNot = new();
    private readonly Dictionary<IFilterItem, DisplayFilter> _filterAll = new();

    private MediaItemM _currentMediaItem;
    private bool _showImages = true;
    private bool _showVideos = true;
    private bool _groupByFolders = true;
    private bool _groupByDate = true;
    private bool _sortByFileFirst = true;
    private string _positionSlashCount;

    public event EventHandler SelectionChangedEventHandler = delegate { };

    public List<MediaItemM> SelectedItems => _selectedItems;
    public List<MediaItemM> LoadedItems { get; } = new();
    public List<MediaItemM> FilteredItems { get; } = new();
    public MediaItemFilterSizeM FilterSize { get; } = new();

    public int FilterAndCount => _filterAnd.Count;
    public int FilterOrCount => _filterOr.Count;
    public int FilterNotCount => _filterNot.Count;
    public int SelectedCount => SelectedItems.Count;

    public bool ShowImages { get => _showImages; set { _showImages = value; OnPropertyChanged(); } }
    public bool ShowVideos { get => _showVideos; set { _showVideos = value; OnPropertyChanged(); } }
    public bool GroupByFolders { get => _groupByFolders; set { _groupByFolders = value; OnPropertyChanged(); } }
    public bool GroupByDate { get => _groupByDate; set { _groupByDate = value; OnPropertyChanged(); } }
    public bool SortByFileFirst { get => _sortByFileFirst; set { _sortByFileFirst = value; OnPropertyChanged(); } }
    public string PositionSlashCount { get => _positionSlashCount; set { _positionSlashCount = value; OnPropertyChanged(); } }

    public bool NeedReload { get; set; }
    public double ThumbScale { get; set; }

    public MediaItemM CurrentMediaItem {
      get => _currentMediaItem;
      set {
        _currentMediaItem = value;

        // TODO temporary
        if (Core.Instance.MediaItemsM.Current != value)
          Core.Instance.MediaItemsM.Current = value;

        OnPropertyChanged();
        UpdatePositionSlashCount();
      }
    }

    public string ActiveFileSize {
      get {
        try {
          var size = SelectedItems.Sum(mi => new FileInfo(mi.FilePath).Length);

          return size == 0 ? string.Empty : IOExtensions.FileSizeToString(size);
        }
        catch {
          return string.Empty;
        }
      }
    }

    public ThumbnailsGridM(double thumbScale) {
      ThumbScale = thumbScale;
    }

    public void UpdatePositionSlashCount() =>
      PositionSlashCount = $"{(CurrentMediaItem == null ? string.Empty : $"{FilteredItems.IndexOf(CurrentMediaItem) + 1}/")}{FilteredItems.Count}";

    public void ClearItBeforeLoad() {
      foreach (var item in SelectedItems)
        item.IsSelected = false;

      SelectedItems.Clear();
      LoadedItems.Clear();
      FilteredItems.Clear();
      SelectionChanged();
      CurrentMediaItem = null;
    }

    private void SelectionChanged() {
      SelectionChangedEventHandler.Invoke(this, EventArgs.Empty);
      OnPropertyChanged(nameof(SelectedCount));
      OnPropertyChanged(nameof(ActiveFileSize));
    }

    public void SetSelected(MediaItemM mi, bool value) => Selecting.SetSelected(_selectedItems, mi, value, SelectionChanged);

    public void UpdateSelected() {
      foreach (var mi in SelectedItems)
        mi.IsSelected = true;

      foreach (var mi in FilteredItems.Except(SelectedItems))
        mi.IsSelected = false;

      CurrentMediaItem = SelectedItems.Count == 1 ? SelectedItems[0] : null;
      SelectionChanged();
    }

    public void Select(MediaItemM mi, bool isCtrlOn, bool isShiftOn) {
      Selecting.Select(_selectedItems, FilteredItems, mi, isCtrlOn, isShiftOn, SelectionChanged);
      CurrentMediaItem = SelectedItems.Count == 1 ? SelectedItems[0] : null;
    }

    public void DeselectAll() {
      foreach (var mi in SelectedItems)
        mi.IsSelected = false;

      CurrentMediaItem = null;
      SelectedItems.Clear();
      SelectionChanged();
    }

    public void SelectAll() {
      foreach (var mi in FilteredItems)
        mi.IsSelected = true;

      CurrentMediaItem = null;
      SelectedItems.Clear();
      SelectedItems.AddRange(FilteredItems);
      SelectionChanged();
    }

    public void Remove(MediaItemM item, bool isCurrent) {
      LoadedItems.Remove(item);

      if (FilteredItems.Remove(item)) {
        NeedReload = true;
        if (isCurrent)
          UpdatePositionSlashCount();
      }

      if (item == CurrentMediaItem)
        CurrentMediaItem = null;

      if (isCurrent)
        SetSelected(item, false);
      else
        SelectedItems.Remove(item);
    }

    public void RemoveSelected() {
      var items = FilteredItems.Where(x => x.IsSelected).ToList();
      CurrentMediaItem = MediaItemsM.GetNewCurrent(FilteredItems, items);

      foreach (var mi in items)
        Remove(mi, true);
    }

    public void Zoom(int delta) {
      if (delta < 0 && ThumbScale < .1) return;
      ThumbScale += delta > 0 ? .05 : -.05;
      ResetThumbsSize();
    }

    public void ResetThumbsSize() {
      foreach (var item in LoadedItems)
        item.SetThumbSize(true);
    }

    public List<MediaItemM> GetSelectedOrAll() => SelectedItems.Count == 0 ? FilteredItems : SelectedItems;

    public void SelectNotModified(HashSet<MediaItemM> modifiedItems) {
      foreach (var mi in FilteredItems)
        SetSelected(mi, !modifiedItems.Contains(mi));

      CurrentMediaItem = null;
    }

    public void Shuffle() {
      FilteredItems.Shuffle();
      GroupByFolders = false;
      GroupByDate = false;
    }

    // TODO use universal function
    public void FilteredItemsSetInPlace(MediaItemM mi) {
      var oldIndex = FilteredItems.IndexOf(mi);
      var newIndex = FilteredItems.OrderBy(x => x.FileName).ToList().IndexOf(mi);
      FilteredItems.RemoveAt(oldIndex);
      FilteredItems.Insert(newIndex, mi);
      UpdatePositionSlashCount();
    }

    public async Task ReloadFilteredItems() {
      FilteredItems.Clear();
      var filtered = Filter(LoadedItems);

      var sorted = SortByFileFirst
        ? filtered.OrderBy(x => x.FileName).ThenBy(
          x => GroupByFolders
            ? x.Folder.FolderKeyword != null
              ? x.Folder.FolderKeyword.FullPath
              : x.Folder.FullPath
            : string.Empty)
        : GroupByFolders
          ? filtered.OrderBy(
            x => x.Folder.FolderKeyword != null
              ? x.Folder.FolderKeyword.FullPath
              : x.Folder.FullPath).ThenBy(x => x.FileName)
          : filtered.OrderBy(x => x.FileName);

      foreach (var mi in sorted)
        FilteredItems.Add(mi);

      if (FilteredItems.IndexOf(CurrentMediaItem) < 0)
        await Core.RunOnUiThread(() => CurrentMediaItem = null);

      UpdatePositionSlashCount();
    }

    public static ThumbnailsGridM ActivateThumbnailsGrid(ThumbnailsGridM oldGrid, ThumbnailsGridM newGrid) {
      if (oldGrid != null)
        foreach (var item in oldGrid._filterAll.Keys)
          item.DisplayFilter = DisplayFilter.None;

      if (newGrid != null)
        foreach (var (k, v) in newGrid._filterAll)
          k.DisplayFilter = v;

      return newGrid;
    }

    public void SetMediaItemFilterSizeRange() {
      var zeroItems = FilteredItems.Count == 0;
      var min = zeroItems ? 0 : FilteredItems.Min(x => x.Width * x.Height);
      var max = zeroItems ? 0 : FilteredItems.Max(x => x.Width * x.Height);
      FilterSize.SetLoadedRange(min, max);
    }

    public void ClearFilters() {
      foreach (var item in _filterAll.Keys.ToArray())
        SetDisplayFilter(item, DisplayFilter.None);
    }

    public void SetDisplayFilter(IFilterItem item, DisplayFilter displayFilter) {
      item.DisplayFilter = item.DisplayFilter != DisplayFilter.None
        ? DisplayFilter.None
        : displayFilter;

      var m = (item as IViewModel<object>)?.ToModel()
        ?? (item as IViewModel<int>)?.ToModel();

      if (m == null) return;

      _filterAll.Remove(item);
      if (item.DisplayFilter != DisplayFilter.None)
        _filterAll.Add(item, item.DisplayFilter);

      switch (item.DisplayFilter) {
        case DisplayFilter.None:
          if (_filterAnd.Remove(m))
            OnPropertyChanged(nameof(FilterAndCount));
          if (_filterOr.Remove(m))
            OnPropertyChanged(nameof(FilterOrCount));
          if (_filterNot.Remove(m))
            OnPropertyChanged(nameof(FilterNotCount));
          break;
        case DisplayFilter.And:
          _filterAnd.Add(m);
          OnPropertyChanged(nameof(FilterAndCount));
          break;
        case DisplayFilter.Or:
          _filterOr.Add(m);
          OnPropertyChanged(nameof(FilterOrCount));
          break;
        case DisplayFilter.Not:
          _filterNot.Add(m);
          OnPropertyChanged(nameof(FilterNotCount));
          break;
        default:
          throw new ArgumentOutOfRangeException(nameof(displayFilter), displayFilter, null);
      }
    }

    public IEnumerable<MediaItemM> Filter(List<MediaItemM> mediaItems) {
      // Media Type
      var mediaTypes = new HashSet<MediaType>();
      if (ShowImages) mediaTypes.Add(MediaType.Image);
      if (ShowVideos) mediaTypes.Add(MediaType.Video);
      mediaItems = mediaItems.Where(mi => mediaTypes.Any(x => x.Equals(mi.MediaType))).ToList();

      // TODO GeoNames

      //Ratings
      var chosenRatings = _filterOr.OfType<int>().ToArray();
      if (chosenRatings.Length > 0)
        mediaItems = mediaItems.Where(mi => mi.IsNew || chosenRatings.Any(x => x.Equals(mi.Rating))).ToList();

      // MediaItemSizes
      if (!FilterSize.AllSizes)
        mediaItems = mediaItems.Where(mi => mi.IsNew || FilterSize.Fits(mi.Width * mi.Height)).ToList();

      // People
      var andPeople = _filterAnd.OfType<PersonM>().ToArray();
      var orPeople = _filterOr.OfType<PersonM>().ToArray();
      var notPeople = _filterNot.OfType<PersonM>().ToArray();
      var andPeopleAny = andPeople.Length > 0;
      var orPeopleAny = orPeople.Length > 0;
      if (orPeopleAny || andPeopleAny || notPeople.Length > 0) {
        mediaItems = mediaItems.Where(mi => {
          if (mi.IsNew)
            return true;
          if (mi.People != null && notPeople.Any(fp => mi.People.Any(p => p == fp)))
            return false;
          if (!andPeopleAny && !orPeopleAny)
            return true;
          if (mi.People != null && andPeopleAny && andPeople.All(fp => mi.People.Any(p => p == fp)))
            return true;
          if (mi.People != null && orPeople.Any(fp => mi.People.Any(p => p == fp)))
            return true;

          return false;
        }).ToList();
      }

      // Keywords
      var andKeywords = _filterAnd.OfType<KeywordM>().ToArray();
      var orKeywords = _filterOr.OfType<KeywordM>().ToArray();
      var notKeywords = _filterNot.OfType<KeywordM>().ToArray();
      var andKeywordsAny = andKeywords.Length > 0;
      var orKeywordsAny = orKeywords.Length > 0;
      if (orKeywordsAny || andKeywordsAny || notKeywords.Length > 0) {
        mediaItems = mediaItems.Where(mi => {
          if (mi.IsNew)
            return true;
          if (mi.Keywords != null && notKeywords.Any(fk => mi.Keywords.Any(mik => mik.FullName.StartsWith(fk.FullName))))
            return false;
          if (!andKeywordsAny && !orKeywordsAny)
            return true;
          if (mi.Keywords != null && andKeywordsAny && andKeywords.All(fk => mi.Keywords.Any(mik => mik.FullName.StartsWith(fk.FullName))))
            return true;
          if (mi.Keywords != null && orKeywords.Any(fk => mi.Keywords.Any(mik => mik.FullName.StartsWith(fk.FullName))))
            return true;
          return false;
        }).ToList();
      }

      return mediaItems;
    }
  }
}
