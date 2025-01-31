﻿using MH.Utils.BaseClasses;
using PictureManager.Domain;
using PictureManager.Domain.Models;
using System;
using System.Windows.Controls;
using System.Windows.Media;

namespace PictureManager.ViewModels {
  public static class MediaItemVideoPreviewVM {
    public static MediaElement VideoPreview { get; }
    public static RelayCommand<Grid> ShowVideoPreviewCommand { get; }
    public static RelayCommand<object> HideVideoPreviewCommand { get; }

    static MediaItemVideoPreviewVM() {
      ShowVideoPreviewCommand = new(ShowVideoPreview);
      HideVideoPreviewCommand = new(HideVideoPreview);

      VideoPreview = new() {
        LoadedBehavior = MediaState.Manual,
        IsMuted = true,
        Stretch = Stretch.Fill
      };

      VideoPreview.MediaEnded += (o, _) => {
        // MediaElement.Stop()/Play() doesn't work when is video shorter than 1s
        ((MediaElement)o).Position = TimeSpan.FromMilliseconds(1);
      };
    }

    private static void ShowVideoPreview(Grid grid) {
      if (grid?.DataContext is not MediaItemM mi || mi.MediaType != MediaType.Video) return;

      var rotation = new TransformGroup();
      rotation.Children.Add(new RotateTransform(mi.RotationAngle));
      (VideoPreview.Parent as Grid)?.Children.Remove(VideoPreview);
      VideoPreview.LayoutTransform = rotation;
      VideoPreview.Source = new(mi.FilePath);
      grid.Children.Insert(2, VideoPreview);
      VideoPreview.Play();
    }

    private static void HideVideoPreview() {
      if (VideoPreview.Source == null) return;

      (VideoPreview.Parent as Grid)?.Children.Remove(VideoPreview);
      VideoPreview.Source = null;
    }
  }
}
