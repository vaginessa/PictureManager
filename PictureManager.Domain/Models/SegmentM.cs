using MH.Utils.BaseClasses;
using MH.Utils.Extensions;
using MH.Utils.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;

namespace PictureManager.Domain.Models {
  public sealed class SegmentM : ObservableObject, IEquatable<SegmentM>, ISelectable {
    private bool _isSelected;

    #region DB Properties
    private PersonM _person;
    private double _x;
    private double _y;
    private double _size;

    public int Id { get; }
    public MediaItemM MediaItem { get; set; }
    public PersonM Person { get => _person; set { _person = value; OnPropertyChanged(); } }
    public List<KeywordM> Keywords { get; set; }

    public double X {
      get => _x;
      set {
        _x = value;

        // bounds check
        if (MediaItem != null) {
          if (value < 0) _x = 0;
          if (value > MediaItem.Width - Size) _x = MediaItem.Width - Size;
        }

        OnPropertyChanged();
      }
    }

    public double Y {
      get => _y;
      set {
        _y = value;

        // bounds check
        if (MediaItem != null) {
          if (value < 0) _y = 0;
          if (value > MediaItem.Height - Size) _y = MediaItem.Height - Size;
        }

        OnPropertyChanged();
      }
    }

    public double Size {
      get => _size;
      set {
        _size = value;

        // bounds check
        if (MediaItem != null) {
          var max = Math.Min(MediaItem.Width, MediaItem.Height);
          _size = value > max ? max : value;
        }

        OnPropertyChanged();
      }
    }
    #endregion DB Properties

    public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }
    public Dictionary<SegmentM, double> Similar { get; set; }
    public double SimMax { get; set; }
    public string FilePathCache => IOExtensions.PathCombine(Path.GetDirectoryName(MediaItem.FilePathCache), $"segment_{GetHashCode().ToString()}.jpg");

    public SegmentM() { }

    public SegmentM(int id, double x, double y, double size) {
      Id = id;
      X = x;
      Y = y;
      Size = size;
    }

    #region IEquatable implementation
    public bool Equals(SegmentM other) => Id == other?.Id;
    public override bool Equals(object obj) => Equals(obj as SegmentM);
    public override int GetHashCode() => Id;
    public static bool operator ==(SegmentM a, SegmentM b) => a?.Equals(b) ?? b is null;
    public static bool operator !=(SegmentM a, SegmentM b) => !(a == b);
    #endregion IEquatable implementation
  }
}
