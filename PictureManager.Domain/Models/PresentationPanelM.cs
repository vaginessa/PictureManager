﻿using MH.Utils;
using MH.Utils.BaseClasses;
using System.Timers;

namespace PictureManager.Domain.Models {
  public sealed class PresentationPanelM : ObservableObject {
    private bool _isRunning;
    private bool _playPanoramicImages = true;
    private bool _isAnimationOn;
    private int _minAnimationDuration;
    private int _interval = 3;
    private readonly Timer _timer = new();
    private readonly MediaViewerM _mediaViewerM;

    public bool IsRunning {
      get => _isRunning;
      set {
        _isRunning = value;
        _timer.Enabled = value;
        OnPropertyChanged();
      }
    }

    public bool PlayPanoramicImages {
      get => _playPanoramicImages;
      set {
        _playPanoramicImages = value;
        OnPropertyChanged();
      }
    }

    public bool IsAnimationOn {
      get => _isAnimationOn;
      set {
        _isAnimationOn = value;
        if (!value)
          Start(_mediaViewerM.Current, false);
        OnPropertyChanged();
      }
    }

    public int MinAnimationDuration { get => _minAnimationDuration; set { _minAnimationDuration = value; OnPropertyChanged(); } }

    public int Interval {
      get => _interval;
      set {
        _interval = value;
        _timer.Interval = value * 1000;
        OnPropertyChanged();
      }
    }

    public bool IsPaused { get; private set; }
    public RelayCommand<object> PresentationCommand { get; set; }

    public PresentationPanelM(MediaViewerM mediaViewerM) {
      _mediaViewerM = mediaViewerM;
      PresentationCommand = new(Presentation);

      _timer.Interval = Interval * 1000;
      _timer.Elapsed += (_, _) => Next();
    }

    ~PresentationPanelM() {
      _timer?.Dispose();
    }

    public void Start(MediaItemM current, bool delay) {
      if (delay
        && PlayPanoramicImages
        && current.MediaType == MediaType.Image 
        && MediaItemsM.IsPanoramic(current)) {
        Pause();
        MinAnimationDuration = Interval * 1000;
        IsAnimationOn = true;
        return;
      }

      IsPaused = false;
      IsRunning = true;
      _mediaViewerM.MediaPlayerM.PlayType = PlayType.Video;
      _mediaViewerM.MediaPlayerM.RepeatForSeconds = Interval;

      if (!delay) Next();
    }

    public void Stop() {
      if (IsAnimationOn)
        IsAnimationOn = false;

      IsPaused = false;
      IsRunning = false;
      _mediaViewerM.MediaPlayerM.RepeatForSeconds = 0; // infinity
    }

    public void Pause() {
      IsPaused = true;
      IsRunning = false;
    }

    private void Presentation() {
      if (IsAnimationOn || IsRunning || IsPaused)
        Stop();
      else
        Start(_mediaViewerM.Current, true);
    }

    private void Next() {
      Tasks.RunOnUiThread(() => {
        if (IsPaused) return;
        if (_mediaViewerM.CanNext())
          _mediaViewerM.Next();
        else
          Stop();
      });
    }

    public void Next(MediaItemM current) {
      var isPanoramic = MediaItemsM.IsPanoramic(current);

      if (IsRunning && (current.MediaType == MediaType.Video || (isPanoramic && PlayPanoramicImages))) {
        Pause();

        if (current.MediaType == MediaType.Image && isPanoramic)
          Start(current, true);
      }
    }
  }
}
