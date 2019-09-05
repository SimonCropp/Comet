﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Comet.Internal;

namespace Comet
{
    public interface IListView
    {
        int Sections();
        int Rows(int section);
        View FooterView();
        View HeaderView();
        object ItemAt(int section, int index);
        View ViewFor(int section, int index);
        View HeaderFor(int section);
        View FooterFor(int section);
        bool ShouldDisposeViews { get; }
        void OnSelected(int section, int index);
    }

    public class ListView<T> : ListView
    {
        //TODO Evaluate if 30 is a good number
        protected IDictionary<object, View> CurrentViews { get; }

        readonly Binding<IReadOnlyList<T>> itemsBinding;
        IReadOnlyList<T> items;

        public ListView(Binding<IReadOnlyList<T>> items) : this()
        {

            this.itemsBinding = items;
            this.items = items.Get();
            SetupObservable();
        }

        public ListView()
        {
            if (HandlerSupportsVirtualization)
            {
                CurrentViews = new FixedSizeDictionary<object, View>(30)
                {
                    OnDequeue = (pair) => pair.Value?.Dispose()
                };
            }
            else
                CurrentViews = new Dictionary<object, View>();

            ShouldDisposeViews = true;
        }
        protected override void ViewPropertyChanged(string property, object value)
        {
            //Update this when things change!
            DisposeObservable();
            items = itemsBinding?.Get();
            SetupObservable();
            base.ViewPropertyChanged(property, value);
        }

        void SetupObservable()
        {
            if (!(items is ObservableCollection<T> observable))
                return;
            observable.CollectionChanged += Observable_CollectionChanged;
        }

        protected virtual void Observable_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            ReloadData();
        }

        void DisposeObservable()
        {
            if (!(items is ObservableCollection<T> observable))
                return;

            observable.CollectionChanged -= Observable_CollectionChanged;
        }


        public Func<T, View> ViewFor { get; set; }

        public Func<int, T> ItemFor { get; set; }

        public Func<int> Count { get; set; }

        protected override int GetCount(int section) => items?.Count() ?? Count?.Invoke() ?? 0;

        protected override object GetItemAt(int section, int index) => items.SafeGetAtIndex(index, ItemFor);

        protected override View GetViewFor(int section, int index)
        {
            var item = (T)GetItemAt(section, index);
            if (item == null)
                return null;
            if (!CurrentViews.TryGetValue(item, out var view) || (view?.IsDisposed ?? true))
            {
                view = ViewFor?.Invoke(item);
                if (view == null)
                    return null;
                CurrentViews[item] = view;
                view.Parent = this;
            }
            return view;
        }

        public override void Add(View view) => throw new NotSupportedException("You cannot add a View directly to a Typed ListView");


        protected override void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            DisposeObservable();

            var currentViews = CurrentViews?.ToList();
            CurrentViews?.Clear();
            currentViews?.ForEach(x => x.Value?.Dispose());
            base.Dispose(disposing);
        }
    }

    public class ListView : View, IEnumerable, IEnumerable<View>, IListView
    {
        public static bool HandlerSupportsVirtualization { get; set; } = true;

        List<View> views;

        public View Header { get; set; }

        public View Footer { get; set; }

        public Action<object> ItemSelected { get; set; }

        protected virtual int GetSections() => 1;

        protected virtual int GetCount(int section) => views?.Count ?? 0;

        protected virtual View GetHeaderFor(int section) => null;

        protected virtual View GetFooterFor(int section) => null;

        protected virtual object GetItemAt(int section, int index) => views?[index];

        protected virtual View GetViewFor(int section, int index) => views?[index];

        public virtual void Add(View view)
        {
            if (views == null)
                views = new List<View>();
            views.Add(view);
        }

        public virtual void ReloadData()
        {
            ViewHandler?.UpdateValue(nameof(ReloadData), null);
        }

        protected virtual void OnSelected(int section, int index)
        {
            var item = GetItemAt(section, index);
            ItemSelected?.Invoke(item);
        }

        protected override void Dispose(bool disposing)
        {
            views?.ForEach(v => v.Dispose());
            //TODO: Verify. I don't think we need to check all active views anymore
            var cells = ActiveViews.Where(x => x.Parent == this).ToList();
            foreach (var cell in cells)
                cell.Dispose();
            base.Dispose(disposing);
        }

        protected override void OnParentChange(View parent)
        {
            base.OnParentChange(parent);
            views?.ForEach(v => v.Parent = parent);
        }
        protected bool ShouldDisposeViews { get; set; }

        bool IListView.ShouldDisposeViews => ShouldDisposeViews;

        IEnumerator<View> IEnumerable<View>.GetEnumerator() => views?.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => views?.GetEnumerator();

        int IListView.Sections() => GetSections();

        int IListView.Rows(int section) => GetCount(section);

        View IListView.FooterView() => Footer;

        View IListView.HeaderView() => Header;

        object IListView.ItemAt(int section, int index) => GetItemAt(section, index);

        View IListView.ViewFor(int section, int index) => GetViewFor(section, index);

        View IListView.HeaderFor(int section) => GetHeaderFor(section);

        View IListView.FooterFor(int section) => GetFooterFor(section);

        void IListView.OnSelected(int section, int index) => OnSelected(section, index);
    }


    public class Section : View
    {
        public View Header { get; set; }
        public View Footer { get; set; }
        public Func<int, View> ViewFor { get; set; }
        public Func<int> Count { get; set; }

        List<View> views;
        public virtual void Add(View view)
        {
            if (views == null)
                views = new List<View>();
            views.Add(view);
        }
        public virtual object GetItemAt(int index) => views?[index];
        public virtual int GetCount() => views?.Count ?? 0;
        public virtual View GetViewFor(int index) => (View)GetItemAt(index);
    }

    public class Section<T> : Section
    {
        readonly Binding<IReadOnlyList<T>> itemsBinding;
        IReadOnlyList<T> items;

        public Section() { }

        public Section(Binding<IReadOnlyList<T>> items)
        {
            this.itemsBinding = items;
            this.items = items.Get();
        }

        protected override void ViewPropertyChanged(string property, object value)
        {
            //Update this when things change!
            items = itemsBinding.Get();
            base.ViewPropertyChanged(property, value);
        }
        public new Func<T, View> ViewFor { get; set; }

        public Func<int, T> ItemFor { get; set; }

        public override object GetItemAt(int index) => items.SafeGetAtIndex(index, ItemFor);
        public override View GetViewFor(int index) => ViewFor?.Invoke((T)GetItemAt(index));
        public override int GetCount() => items?.Count ?? Count?.Invoke() ?? 0;

    }

    public class SectionedListView<T> : ListView<T>
    {

        public override void Add(View view) => throw new NotSupportedException("You cannot add a View directly to a SectionedListView");

        List<Section<T>> sections;
        public virtual void Add(Section<T> section)
        {
            if (sections == null)
                sections = new List<Section<T>>();
            sections.Add(section);
        }

        public Func<int, Section<T>> SectionFor { get; set; }

        public Func<int> SectionCount { get; set; }


        protected override int GetSections() => sections?.Count() ?? SectionCount?.Invoke() ?? 0;
        protected override View GetHeaderFor(int section) => sections.SafeGetAtIndex(section, SectionFor)?.Header;
        protected override View GetFooterFor(int section) => sections.SafeGetAtIndex(section, SectionFor)?.Footer;
        protected override object GetItemAt(int section, int index) => sections.SafeGetAtIndex(section, SectionFor)?.GetItemAt(index);
        protected override int GetCount(int section) => sections.SafeGetAtIndex(section, SectionFor)?.GetCount() ?? 0;

        protected override View GetViewFor(int section, int index)
        {
            var item = (T)GetItemAt(section, index);
            if (item == null)
                return null;
            if (!CurrentViews.TryGetValue(item, out var view) || (view?.IsDisposed ?? true))
            {
                view = sections.SafeGetAtIndex(section, SectionFor)?.GetViewFor(index);
                if (view == null)
                    return null;
                CurrentViews[item] = view;
                view.Parent = this;
            }
            return view;

        }
    }

    public class SectionedListView : ListView
    {
        public override void Add(View view) => throw new NotSupportedException("You cannot add a View directly to a SectionedListView");

        List<Section> sections;
        public virtual void Add(Section section)
        {
            if (sections == null)
                sections = new List<Section>();
            sections.Add(section);
        }
        protected override int GetSections() => sections?.Count() ?? 0;
        protected override View GetHeaderFor(int section) => sections?[section]?.Header;
        protected override View GetFooterFor(int section) => sections?[section]?.Footer;
        protected override object GetItemAt(int section, int index) => sections?[section]?.GetItemAt(index);
        protected override int GetCount(int section) => sections?[section]?.GetCount() ?? 0;
        protected override View GetViewFor(int section, int index) => sections?[section]?.GetViewFor(index);
    }
}
