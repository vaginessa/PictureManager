﻿using System;
using MH.Utils.BaseClasses;

namespace PictureManager.Domain.Models {
  public sealed class MediaItemFilterSizeM : ObservableObject {
    private double _pixelMin;
    private double _pixelMax;
    private double _min;
    private double _max;

    public double PixelMin { get => _pixelMin; set { _pixelMin = value; OnPropertyChanged(); } }
    public double PixelMax { get => _pixelMax; set { _pixelMax = value; OnPropertyChanged(); } }
    public double Min { get => _min; set { _min = value; OnPropertyChanged(); } }
    public double Max { get => _max; set { _max = value; OnPropertyChanged(); } }

    public bool AllSizes { get; set; } = true;
    public bool Fits(int size) => size >= PixelMin && size <= PixelMax;

    public void SetLoadedRange(int min, int max) {
      PixelMin = Math.Round(min / 1000000.0, 1) * 1000000;
      PixelMax = Math.Round((max + 100000) / 1000000.0, 1) * 1000000;
      if (!AllSizes) {
        AllSizes = true;
        return;
      }
      Min = PixelMin;
      Max = PixelMax;
    }
  }
}
