﻿using MahApps.Metro.Controls;
using PictureManager.Domain.Models;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PictureManager.UserControls {
  public partial class PersonFacesControl : INotifyPropertyChanged {
    public event PropertyChangedEventHandler PropertyChanged;
    public void OnPropertyChanged([CallerMemberName] string name = null) =>
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private Person _person;

    public ObservableCollection<Face> AllPersonFaces { get; } = new();
    public Person Person { get => _person; set { _person = value; OnPropertyChanged(); } }

    public PersonFacesControl() {
      InitializeComponent();

      AttachEvents();
    }

    private void AttachEvents() {
      AllowDrop = true;
      PreviewMouseLeftButtonDown += SetDragObject;
      PreviewMouseLeftButtonUp += ReleaseDragObject;
      MouseMove += StartDragDrop;
      DragEnter += AllowDropCheck;
      DragLeave += AllowDropCheck;
      DragOver += AllowDropCheck;
      Drop += OnDrop;

      BtnClose.Click += (o, e) => Visibility = Visibility.Collapsed;
    }

    public async Task ReloadPersonFacesAsync(Person person) {
      Person = person;
      AllPersonFaces.Clear();
      Visibility = Visibility.Visible;

      await Task.Run(async () => {
        foreach (var face in App.Core.Faces.All.Cast<Face>().Where(x => x.PersonId == person.Id)) {
          await face.SetPictureAsync(App.Core.Faces.FaceSize);
          face.MediaItem.SetThumbSize();
          await App.Core.RunOnUiThread(() => {
            AllPersonFaces.Add(face);
          });
        }
      });

      IcTopFaces.FindChild<ScrollViewer>().ScrollToHome();
      IcAllFaces.FindChild<ScrollViewer>().ScrollToHome();
    }

    private void MouseWheelScroll(object sender, MouseWheelEventArgs e) {
      var sv = (ScrollViewer)sender;
      sv.ScrollToHorizontalOffset(sv.ContentHorizontalOffset + e.Delta * - 1);
      e.Handled = true;
    }

    #region Drag & Drop

    private Point _dragDropStartPosition;
    private FrameworkElement _dragDropSource;
    private DragDropEffects _dragDropEffects;

    private void SetDragObject(object sender, MouseButtonEventArgs e) {
      _dragDropSource = null;
      _dragDropStartPosition = new Point(0, 0);

      var src = (FrameworkElement)e.OriginalSource;
      if (src.DataContext is not Face) return;

      _dragDropSource = src;
      _dragDropEffects = DragDropEffects.Copy;
      _dragDropStartPosition = e.GetPosition(null);
    }

    private void ReleaseDragObject(object sender, MouseButtonEventArgs e) => _dragDropSource = null;

    private void StartDragDrop(object sender, MouseEventArgs e) {
      if (_dragDropSource == null || !IsDragDropStarted(e)) return;
      DragDrop.DoDragDrop(_dragDropSource, _dragDropSource.DataContext, _dragDropEffects);
    }

    private bool IsDragDropStarted(MouseEventArgs e) {
      if (e.LeftButton != MouseButtonState.Pressed) return false;
      var diff = _dragDropStartPosition - e.GetPosition(null);
      return Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
             Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance;
    }

    private void AllowDropCheck(object sender, DragEventArgs e) {
      var dest = (FrameworkElement)e.OriginalSource;
      var isFromTop = _dragDropSource.IsDescendantOf(IcTopFaces);
      var isInTop = dest.IsDescendantOf(IcTopFaces);
      var face = e.Data.GetData(typeof(Face)) as Face;

      if (face != null
        && ((isInTop && (Person.Faces == null || !Person.Faces.Contains(face)))
        || (isFromTop && !isInTop && _dragDropSource != dest))) return;

      // can't be dropped
      e.Effects = DragDropEffects.None;
      e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e) {
      var dest = (FrameworkElement)e.OriginalSource;
      var face = e.Data.GetData(typeof(Face)) as Face;
      var dropInTop = dest.IsDescendantOf(IcTopFaces);

      if (face == null) return;

      if (dropInTop) {
        if (Person.Faces == null) {
          Person.Faces = new();
          Person.OnPropertyChanged(nameof(Person.Faces));
        }
        Person.Faces.Add(face);
      }
      else {
        Person.Faces.Remove(face);
        if (Person.Faces.Count == 0)
          Person.Faces = null;
      }

      App.Db.SetModified<People>();
    }

    #endregion
  }
}
