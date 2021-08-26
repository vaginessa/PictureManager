﻿using PictureManager.Domain.CatTreeViewModels;
using SimpleDB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PictureManager.Domain.Models {
  public sealed class Keywords : BaseCatTreeViewCategory, ITable, ICatTreeViewCategory {
    public TableHelper Helper { get; set; }
    public List<IRecord> All { get; } = new();
    public Dictionary<int, Keyword> AllDic { get; set; }

    private ICatTreeViewGroup _autoAddedGroup;

    public Keywords() : base(Category.Keywords) {
      Title = "Keywords";
      IconName = IconName.TagLabel;
      CanHaveGroups = true;
      CanHaveSubItems = true;
      CanCreateItems = true;
      CanRenameItems = true;
      CanDeleteItems = true;
      CanMoveItem = true;
    }

    public void LoadFromFile() {
      All.Clear();
      AllDic = new Dictionary<int, Keyword>();
      Helper.LoadFromFile();
    }

    public void NewFromCsv(string csv) {
      // ID|Name|Parent
      var props = csv.Split('|');
      if (props.Length != 3) throw new ArgumentException("Incorrect number of values.", csv);
      var keyword = new Keyword(int.Parse(props[0]), props[1], null) { Csv = props };
      All.Add(keyword);
      AllDic.Add(keyword.Id, keyword);
    }

    public void LinkReferences() {
      // ID|Name|Parent
      // MediaItems to the Keyword are added in LinkReferences on MediaItem

      // link hierarchical keywords
      foreach (var keyword in All.Cast<Keyword>()) {
        // reference to parent and back reference to children
        if (!string.IsNullOrEmpty(keyword.Csv[2])) {
          keyword.Parent = AllDic[int.Parse(keyword.Csv[2])];
          keyword.Parent.Items.Add(keyword);
        }

        // csv array is not needed any more
        keyword.Csv = null;
      }

      Items.Clear();
      LoadGroupsAndItems(All);

      // group for keywords automatically added from MediaItems metadata
      _autoAddedGroup = Items.OfType<ICatTreeViewGroup>().SingleOrDefault(x => x.Title.Equals("Auto Added")) ??
                       GroupCreate(this, "Auto Added");
    }

    public Keyword GetByFullPath(string fullPath) {
      if (string.IsNullOrEmpty(fullPath)) return null;

      return Core.Instance.RunOnUiThread(() => {
        var pathNames = fullPath.Split('/');

        // get top level Keyword => Parent is not Keyword but Keywords or CategoryGroup
        var keyword = All.Cast<Keyword>().SingleOrDefault(x => !(x.Parent is Keyword) && x.Title.Equals(pathNames[0]));

        // return Keyword if it was found and is 1 level type
        if (keyword != null && pathNames.Length == 1)
          return keyword;

        // set root as => Parent of the first Keyword from fullPath (or) CategoryGroup "Auto Added"
        var root = keyword?.Parent ?? _autoAddedGroup;

        // for each keyword in pathNames => find or create
        foreach (var name in pathNames)
          root = root.Items.OfType<Keyword>().SingleOrDefault(x => x.Title.Equals(name)) ?? ItemCreate(root, name);

        return root as Keyword;
      }).Result;
    }

    public override ICatTreeViewItem ItemCreate(ICatTreeViewItem root, string name) {
      var item = new Keyword(Helper.GetNextId(), name, root);
      var idx = CatTreeViewUtils.SetItemInPlace(root, item);
      var allIdx = Core.GetAllIndexBasedOnTreeOrder(All, root, idx);

      All.Insert(allIdx, item);
      Core.Instance.Sdb.SetModified<Keywords>();
      if (root is ICatTreeViewGroup)
        Core.Instance.Sdb.SetModified<CategoryGroups>();

      Core.Instance.Sdb.SaveIdSequences();

      return item;
    }

    public override void ItemDelete(ICatTreeViewItem item) {
      if (item is not Keyword keyword) return;

      // remove Keyword from the tree
      item.Parent.Items.Remove(item);

      if (item.Parent is CategoryGroup)
        Core.Instance.Sdb.SetModified<CategoryGroups>();

      // get all descending keywords
      var keywords = new List<ICatTreeViewItem>();
      CatTreeViewUtils.GetThisAndItemsRecursive(keyword, ref keywords);

      // remove Keywords from People
      foreach (var person in Core.Instance.People.All.Cast<Person>().Where(p => p.Keywords != null && p.Keywords.Any(k => keywords.Contains(k))))
        foreach (var k in keywords.Cast<Keyword>())
          if (person.Keywords.Remove(k)) {
            if (!person.Keywords.Any())
              person.Keywords = null;
            person.UpdateDisplayKeywords();
            Core.Instance.Sdb.SetModified<People>();
          }

      foreach (var k in keywords.Cast<Keyword>()) {
        // remove Keyword from MediaItems
        if (k.MediaItems.Count > 0) {
          foreach (var mi in k.MediaItems) {
            mi.Keywords.Remove(k);
            if (mi.Keywords.Count == 0)
              mi.Keywords = null;
          }
          Core.Instance.Sdb.SetModified<MediaItems>();
        }

        k.Parent = null;

        // remove Keyword from DB
        All.Remove(k);
        Core.Instance.Sdb.SetModified<Keywords>();
      }
    }

    /// <summary>
    /// Toggle Keyword on Media Item
    /// </summary>
    /// <param name="k">Keyword</param>
    /// <param name="mi">Media Item</param>
    public static void Toggle(Keyword k, MediaItem mi) {
      if (!k.IsMarked && mi.Keywords != null) {
        mi.Keywords?.Remove(k);
        k.MediaItems.Remove(mi);
        if (mi.Keywords?.Count == 0)
          mi.Keywords = null;
        return;
      }

      if (mi.Keywords != null) {
        // skip if any Parent of MediaItem Keywords is marked Keyword
        var skip = false;
        foreach (var miKeyword in mi.Keywords) {
          var tmpMik = miKeyword;
          while (tmpMik.Parent is Keyword parent) {
            tmpMik = parent;
            if (!parent.Id.Equals(k.Id)) continue;
            skip = true;
            break;
          }
        }

        if (skip) return;

        // remove possible redundant keywords 
        // example: if marked keyword is "Weather/Sunny" keyword "Weather" is redundant
        foreach (var miKeyword in mi.Keywords.ToArray()) {
          var tmpMarkedK = k;
          while (tmpMarkedK.Parent is Keyword parent) {
            tmpMarkedK = parent;
            if (!parent.Id.Equals(miKeyword.Id)) continue;
            mi.Keywords.Remove(miKeyword);
            miKeyword.MediaItems.Remove(mi);
          }
        }
      }

      mi.Keywords ??= new();
      mi.Keywords.Add(k);
      k.MediaItems.Add(mi);
    }
  }
}
