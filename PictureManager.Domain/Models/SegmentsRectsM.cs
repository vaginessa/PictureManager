﻿using MH.Utils.BaseClasses;
using MH.Utils.Dialogs;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace PictureManager.Domain.Models {
  public sealed class SegmentsRectsM : ObservableObject {
    private double _startX;
    private double _startY;
    private bool _isNew;
    private bool _isCurrentModified;
    private SegmentEditMode _editMode;

    private double _scale;
    private bool _isEditOn;
    private bool _areVisible;
    private MediaItemM _mediaItemM;

    public double Scale { get => _scale; set { _scale = value; OnPropertyChanged(); } }
    public bool IsEditOn { get => _isEditOn; set { _isEditOn = value; OnPropertyChanged(); } }
    public bool AreVisible { get => _areVisible; set { _areVisible = value; OnPropertyChanged(); } }

    public MediaItemM MediaItem {
      get => _mediaItemM;
      set {
        _mediaItemM = value;
        OnPropertyChanged();
        ReloadMediaItemSegmentRects();
      }
    }

    public SegmentsM SegmentsM { get; }
    public SegmentRectM Current { get; set; }
    public ObservableCollection<SegmentRectM> MediaItemSegmentsRects { get; } = new();
    public ObservableCollection<Tuple<int, int, int, bool>> SegmentToolTipRects { get; } = new();
    public RelayCommand<SegmentM> SegmentToolTipReloadCommand { get; }

    public SegmentsRectsM(SegmentsM segmentsM) {
      SegmentsM = segmentsM;
      SegmentToolTipReloadCommand = new(SegmentToolTipReload);
    }

    public void CreateNew(double x, double y) {
      MousePosToRawImage(ref x, ref y, Scale, MediaItem);
      _isNew = true;
      _startX = x;
      _startY = y;
      _editMode = SegmentEditMode.ResizeEdge;
      _isCurrentModified = true;
      Current = new(SegmentsM.AddNewSegment(x, y, 0, MediaItem), Scale);
      MediaItemSegmentsRects.Add(Current);
    }

    public void SetCurrent(SegmentRectM current, double x, double y) {
      Current = current;
      MousePosToRawImage(ref x, ref y, Scale, MediaItem);
      _editMode = GetEditMode(x, y, Current.Segment);
      SegmentsM.Select(null, current.Segment, false, false);
    }

    private SegmentEditMode GetEditMode(double x, double y, SegmentM segment) {
      var xDiff = Math.Abs(segment.X + (segment.Size / 2) - x);
      var yDiff = Math.Abs(segment.Y + (segment.Size / 2) - y);
      var limit = 10;

      if (xDiff < limit && yDiff < limit && segment.Size > 20)
        return SegmentEditMode.Move;

      if (Math.Abs(xDiff - yDiff) < limit)
        return SegmentEditMode.ResizeCorner;
      else
        return GetResizeEdgeEditMode(x, y, segment);
    }

    private SegmentEditMode GetResizeEdgeEditMode(double x, double y, SegmentM segment) {
      var edge = SegmentEditMode.ResizeLeftEdge;

      if (_isNew) {
        edge = Math.Abs(_startX - x) > Math.Abs(_startY - y)
          ? _startX > x
            ? SegmentEditMode.ResizeLeftEdge
            : SegmentEditMode.ResizeRightEdge
          : _startY > y
            ? SegmentEditMode.ResizeTopEdge
            : SegmentEditMode.ResizeBottomEdge;

        if (Current.Size > 50)
          _isNew = false;
      }
      else {
        var lDiff = Math.Abs(x - segment.X);
        var rDiff = Math.Abs(x - segment.X - segment.Size);
        var tDiff = Math.Abs(y - segment.Y);
        var bDiff = Math.Abs(y - segment.Y - segment.Size);
        var minDiff = (new double[] { lDiff, rDiff, tDiff, bDiff }).Min();

        if (lDiff == minDiff)
          edge = SegmentEditMode.ResizeLeftEdge;
        else if (bDiff == minDiff)
          edge = SegmentEditMode.ResizeBottomEdge;
        else if (tDiff == minDiff)
          edge = SegmentEditMode.ResizeTopEdge;
        else if (rDiff == minDiff)
          edge = SegmentEditMode.ResizeRightEdge;
      }

      return edge;
    }

    public void Edit(double x, double y) {
      var segment = Current.Segment;

      MousePosToRawImage(ref x, ref y, Scale, MediaItem);
      if (x < 0) x = 0;
      if (y < 0) y = 0;
      if (x > MediaItem.Width) x = MediaItem.Width;
      if (y > MediaItem.Height) y = MediaItem.Height;

      _isCurrentModified = true;
      if (!IsEditOn) IsEditOn = true;
      if (_isNew) _editMode = GetResizeEdgeEditMode(x, y, segment);

      switch (_editMode) {
        case SegmentEditMode.Move:
          segment.X = x - (segment.Size / 2);
          segment.Y = y - (segment.Size / 2);
          break;

        case SegmentEditMode.ResizeLeftEdge:
          segment.Size = segment.X + segment.Size - x;
          segment.Y = y - (segment.Size / 2);
          segment.X = x;
          break;

        case SegmentEditMode.ResizeTopEdge:
          segment.Size = segment.Y + segment.Size - y;
          segment.X = x - (segment.Size / 2);
          segment.Y = y;
          break;

        case SegmentEditMode.ResizeRightEdge:
          segment.Size = x - segment.X;
          segment.Y = y - (segment.Size / 2);
          break;

        case SegmentEditMode.ResizeBottomEdge:
          segment.Size = y - segment.Y;
          segment.X = x - (segment.Size / 2);
          break;

        case SegmentEditMode.ResizeEdge:
          break;

        case SegmentEditMode.ResizeCorner:
          var diff = x - segment.X;
          if (x > segment.X + (segment.Size / 2))
            diff = segment.Size - diff;

          segment.X += diff;
          segment.Size -= diff * 2;
          segment.Y += diff;
          break;
      }

      Current.OnPropertyChanged(nameof(Current.X));
      Current.OnPropertyChanged(nameof(Current.Y));
      Current.OnPropertyChanged(nameof(Current.Size));
    }

    public void EndEdit() {
      if (Current == null) return;

      if (_isCurrentModified) {
        SegmentsM.DataAdapter.IsModified = true;
        File.Delete(Current.Segment.FilePathCache);
        Current.Segment.OnPropertyChanged(nameof(Current.Segment.FilePathCache));
        _isCurrentModified = false;
        _isNew = false;
        IsEditOn = false;
      }

      Current = null;
    }

    public void Delete(SegmentRectM item) {
      if (Core.DialogHostShow(new MessageDialog(
        "Delete Segment",
        "Do you really want to delete this segment?",
        Res.IconQuestion,
        true)) != 1) return;

      SegmentsM.Delete(item.Segment);
      MediaItemSegmentsRects.Remove(item);
    }

    public static void MousePosToRawImage(ref double x, ref double y, double scale, MediaItemM mediaItem) {
      var mX = x / scale;
      var mY = y / scale;

      switch ((MediaOrientation)mediaItem.Orientation) {
        case MediaOrientation.Rotate180:
          x = mediaItem.Width - mX;
          y = mediaItem.Height - mY;
          break;
        case MediaOrientation.Rotate270:
          x = mY;
          y = mediaItem.Height - mX;
          break;
        case MediaOrientation.Rotate90:
          x = mediaItem.Width - mY;
          y = mX;
          break;
        default:
          x = mX;
          y = mY;
          break;
      }
    }

    private void ReloadMediaItemSegmentRects() {
      Current = null;
      MediaItemSegmentsRects.Clear();
      if (MediaItem?.Segments == null) return;

      SegmentsM.Selected.DeselectAll();

      foreach (var segment in MediaItem.Segments)
        MediaItemSegmentsRects.Add(new(segment, Scale));
    }

    public void UpdateScale(double scale) {
      Scale = scale;

      foreach (var sr in MediaItemSegmentsRects)
        sr.Scale = Scale;
    }

    private void SegmentToolTipReload(SegmentM segment) {
      SegmentToolTipRects.Clear();
      if (segment?.MediaItem?.Segments == null) return;

      segment.MediaItem.SetThumbSize();
      segment.MediaItem.SetInfoBox();

      var rotated = segment.MediaItem.Orientation is 6 or 8;
      var scale = rotated
        ? segment.MediaItem.Height / (double)segment.MediaItem.ThumbWidth
        : segment.MediaItem.Width / (double)segment.MediaItem.ThumbWidth;

      foreach (var s in segment.MediaItem.Segments) {
        double rX = s.X;
        double rY = s.Y;

        switch ((MediaOrientation)s.MediaItem.Orientation) {
          case MediaOrientation.Rotate180:
            rX = s.MediaItem.Width - s.X - s.Size;
            rY = s.MediaItem.Height - s.Y - s.Size;
            break;
          case MediaOrientation.Rotate270:
            rX = s.MediaItem.Height - s.Y - s.Size;
            rY = s.X;
            break;
          case MediaOrientation.Rotate90:
            rX = s.Y;
            rY = s.MediaItem.Width - s.X - s.Size;
            break;
        }

        SegmentToolTipRects.Add(new(
          (int)(rX / scale),
          (int)(rY / scale),
          (int)(s.Size / scale),
          s == segment));
      }
    }
  }
}
