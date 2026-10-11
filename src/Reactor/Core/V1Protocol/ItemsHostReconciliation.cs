using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Microsoft.UI.Xaml;

// A third-party managed control may forward this list to a native ItemsRepeater.
// CsWinRT cannot see that indirect call site in the consuming application.
[assembly: WinRT.GeneratedWinRTExposedExternalType(typeof(ObservableCollection<object>))]

namespace Microsoft.UI.Reactor.Core.V1Protocol;

internal sealed class ItemsHostState
{
    internal sealed record Item(object? Source, object? Value);
    internal List<Item> Items { get; set; } = new();
}

/// <summary>Shared flat-items lifecycle for descriptors and hand-coded handlers.</summary>
internal static class ItemsHostReconciliation
{
    internal static void Bind<TElement, TControl>(
        ItemsHost<TElement, TControl> strategy, TElement element, TControl control,
        Reconciler reconciler, Action requestRerender, bool isMount)
        where TElement : Element
        where TControl : UIElement
    {
        var state = Reconciler.GetOrCreateReactorState(control);
        // Hand-coded handlers dispatch children after the adapter's initial tag
        // pass. Root this host too, so callback-free parents receive teardown.
        state.Element = element;
        var collection = strategy.GetCollection(control);
        if (isMount)
        {
            if (strategy.SetCollection is { } set && collection is not INotifyCollectionChanged)
            {
                collection = new ObservableCollection<object>();
                set(control, collection);
            }
            else if (collection.Count > 0)
                collection.Clear();
            state.ItemsHostState = new ItemsHostState();
        }

        var itemsState = state.ItemsHostState ??= new ItemsHostState();
        var previous = itemsState.Items;
        var used = new bool[previous.Count];
        var source = strategy.GetItems(element);
        var next = new List<ItemsHostState.Item>(source.Count);
        var equals = strategy.ItemEquals ?? object.Equals;
        var keyed = new Dictionary<string, Queue<int>>(StringComparer.Ordinal);
        for (int i = 0; i < previous.Count; i++)
        {
            if (previous[i].Source is Element { Key: { } key })
            {
                if (!keyed.TryGetValue(key, out var indices))
                    keyed[key] = indices = new Queue<int>();
                indices.Enqueue(i);
            }
        }

        for (int i = 0; i < source.Count; i++)
        {
            var item = source[i];
            int match = -1;
            if (item is Element child)
            {
                if (child.Key is { } key)
                {
                    if (keyed.TryGetValue(key, out var indices) && indices.Count > 0)
                        match = indices.Dequeue();
                }
                else if (i < previous.Count && !used[i] && previous[i].Source is Element { Key: null })
                    match = i;

                var old = match < 0 ? null : previous[match];
                if (match >= 0) used[match] = true;
                var existing = old?.Value as UIElement;
                if (old?.Source is Element oldElement && !reconciler.CanUpdate(oldElement, child))
                {
                    // Detach before teardown: pooling must not rent a still-parented control.
                    RemoveReference(collection, existing);
                    if (existing is not null) reconciler.UnmountChild(existing);
                    existing = null;
                    old = null;
                }
                // Do not equality-skip this descent: an unchanged child Element can
                // contain a component whose own state or inherited context changed.
                var mounted = reconciler.ReconcileV1Child(old?.Source as Element, child, existing, requestRerender);
                if (!ReferenceEquals(existing, mounted)) RemoveReference(collection, existing);
                next.Add(new(item, mounted));
            }
            else
            {
                for (int j = 0; j < previous.Count; j++)
                {
                    if (!used[j] && previous[j].Source is not Element && equals(previous[j].Source, item))
                    {
                        match = j;
                        break;
                    }
                }
                if (match >= 0) used[match] = true;
                next.Add(new(item, match < 0 ? item : previous[match].Value));
            }
        }

        for (int i = 0; i < previous.Count; i++)
        {
            if (used[i]) continue;
            var old = previous[i];
            RemoveReference(collection, old.Value);
            if (old.Source is Element && old.Value is UIElement child)
                reconciler.UnmountChild(child);
        }

        int target = 0;
        foreach (var item in next)
        {
            if (item.Value is not { } value) continue;
            if (target < collection.Count && ReferenceEquals(collection[target], value))
            {
                target++;
                continue;
            }
            int current = IndexOfReference(collection, value, target);
            if (current < 0)
                collection.Insert(target, value);
            else if (collection is ObservableCollection<object> observable)
                observable.Move(current, target);
            else
            {
                collection.RemoveAt(current);
                collection.Insert(target, value);
            }
            target++;
        }
        itemsState.Items = next;
    }

    internal static void Unbind<TElement, TControl>(
        ItemsHost<TElement, TControl> strategy, TControl control, Reconciler reconciler)
        where TElement : Element
        where TControl : UIElement
    {
        var state = Reconciler.GetOrCreateReactorState(control);
        var items = state.ItemsHostState;
        state.ItemsHostState = null;
        strategy.GetCollection(control).Clear();
        if (items is null) return;
        foreach (var item in items.Items)
            if (item.Source is Element && item.Value is UIElement child)
                reconciler.UnmountChild(child);
    }

    private static int IndexOfReference(IList<object> collection, object value, int start)
    {
        for (int i = start; i < collection.Count; i++)
            if (ReferenceEquals(collection[i], value)) return i;
        return -1;
    }

    private static void RemoveReference(IList<object> collection, object? value)
    {
        if (value is null) return;
        int index = IndexOfReference(collection, value, 0);
        if (index >= 0) collection.RemoveAt(index);
    }
}
