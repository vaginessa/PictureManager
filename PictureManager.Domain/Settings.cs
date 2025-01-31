﻿using MH.Utils.BaseClasses;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System;
using MH.Utils;

namespace PictureManager.Domain {
  public sealed class Settings : ObservableObject {
    private int _jpegQualityLevel = 80;
    private string _cachePath = ":\\Temp\\PictureManagerCache";
    private int _thumbnailSize = 400;
    private string _directorySelectFolders;
    private string _ffmpegPath;
    private int _imagesToVideoHeight = 1080;
    private int _imagesToVideoQuality = 27;
    private double _imagesToVideoSpeed = 0.25;
    private string _geoNamesUserName;

    public int JpegQualityLevel { get => _jpegQualityLevel; set { _jpegQualityLevel = value; OnPropertyChanged(); } }
    public string CachePath { get => _cachePath; set { OnCachePathChange(value); } }
    public int ThumbnailSize { get => _thumbnailSize; set { _thumbnailSize = value; OnPropertyChanged(); } }
    public string DirectorySelectFolders { get => _directorySelectFolders; set { _directorySelectFolders = value; OnPropertyChanged(); } }
    public string FfmpegPath { get => _ffmpegPath; set { _ffmpegPath = value; OnPropertyChanged(); } }
    public int ImagesToVideoHeight { get => _imagesToVideoHeight; set { _imagesToVideoHeight = value; OnPropertyChanged(); } }
    public int ImagesToVideoQuality { get => _imagesToVideoQuality; set { _imagesToVideoQuality = value; OnPropertyChanged(); } }
    public double ImagesToVideoSpeed { get => _imagesToVideoSpeed; set { _imagesToVideoSpeed = value; OnPropertyChanged(); } }
    public string GeoNamesUserName { get => _geoNamesUserName; set { _geoNamesUserName = value; OnPropertyChanged(); } }

    public string SettingsFileName { get; set; } = Path.Combine("db", "settings.csv");

    public bool Save() {
      try {
        using var sw = new StreamWriter(SettingsFileName, false, Encoding.UTF8, 65536);
        sw.WriteLine($"{nameof(JpegQualityLevel)}|{JpegQualityLevel}");
        sw.WriteLine($"{nameof(CachePath)}|{CachePath}");
        sw.WriteLine($"{nameof(ThumbnailSize)}|{ThumbnailSize}");
        sw.WriteLine($"{nameof(DirectorySelectFolders)}|{DirectorySelectFolders}");
        sw.WriteLine($"{nameof(FfmpegPath)}|{FfmpegPath}");
        sw.WriteLine($"{nameof(ImagesToVideoHeight)}|{ImagesToVideoHeight}");
        sw.WriteLine($"{nameof(ImagesToVideoQuality)}|{ImagesToVideoQuality}");
        sw.WriteLine($"{nameof(ImagesToVideoSpeed)}|{ImagesToVideoSpeed}");
        sw.WriteLine($"{nameof(GeoNamesUserName)}|{GeoNamesUserName}");

        return true;
      }
      catch (Exception ex) {
        Log.Error(ex);
        return false;
      }
    }

    public bool Load() {
      if (!File.Exists(SettingsFileName)) return false;
      try {
        var props = new Dictionary<string, string>();
        using var sr = new StreamReader(SettingsFileName, Encoding.UTF8);
        string line;

        while ((line = sr.ReadLine()) != null) {
          var prop = line.Split('|');
          if (prop.Length != 2)
            throw new ArgumentException("Incorrect number of values.", line);

          props.Add(prop[0], prop[1]);
        }

        if (props.TryGetValue(nameof(JpegQualityLevel), out var jpegQualityLevel))
          JpegQualityLevel = int.Parse(jpegQualityLevel);
        if (props.TryGetValue(nameof(CachePath), out var cachePath))
          CachePath = cachePath;
        if (props.TryGetValue(nameof(ThumbnailSize), out var thumbnailSize))
          ThumbnailSize = int.Parse(thumbnailSize);
        if (props.TryGetValue(nameof(DirectorySelectFolders), out var directorySelectFolders))
          DirectorySelectFolders = directorySelectFolders;
        if (props.TryGetValue(nameof(FfmpegPath), out var ffmpegPath))
          FfmpegPath = ffmpegPath;
        if (props.TryGetValue(nameof(ImagesToVideoHeight), out var imagesToVideoHeight))
          ImagesToVideoHeight = int.Parse(imagesToVideoHeight);
        if (props.TryGetValue(nameof(ImagesToVideoQuality), out var imagesToVideoQuality))
          ImagesToVideoQuality = int.Parse(imagesToVideoQuality);
        if (props.TryGetValue(nameof(ImagesToVideoSpeed), out var imagesToVideoSpeed))
          ImagesToVideoSpeed = double.Parse(imagesToVideoSpeed);
        if (props.TryGetValue(nameof(GeoNamesUserName), out var geoNamesUserName))
          GeoNamesUserName = geoNamesUserName;

        return true;
      }
      catch {
        // ignored
        return false;
      }
    }

    private void OnCachePathChange(string value) {
      if (value.Length < 4 || !value.StartsWith(":\\") || !value.EndsWith("\\"))
        return;

      _cachePath = value;
      OnPropertyChanged(nameof(CachePath));
    }
  }
}
