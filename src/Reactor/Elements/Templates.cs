using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;

namespace Microsoft.UI.Reactor;

/// <summary>Experimental factories for native templates with declarative, static layouts.</summary>
public static class Templates
{
    /// <summary>
    /// Builds each native template instance from ordinary Reactor elements.
    /// Keep the returned owner stable and dispose it when the consuming control
    /// is permanently removed. Native bindings continue to own data updates.
    /// </summary>
    public static DeclarativeDataTemplate Data(Func<Element> build) => new(build);
}

/// <summary>
/// Owns a native DataTemplate and its realized Reactor trees. This prototype
/// supports Stack/Grid/Border layouts, text and icons, not components or hooks.
/// It does not capture the declaring component's providers or re-render closure.
/// Create, use and dispose on the UI thread; detach consumers before disposal.
/// </summary>
public sealed class DeclarativeDataTemplate : IDisposable
{
    // Keep ownership on the native object, not only on a CsWinRT wrapper which
    // may be collected and subsequently projected as a different managed object.
    private static readonly DependencyProperty InstanceProperty = DependencyProperty.RegisterAttached(
        "DeclarativeTemplateInstance", typeof(object), typeof(DeclarativeDataTemplate), new PropertyMetadata(null));

    private readonly Func<Element> _build;
    private readonly List<WeakReference<Instance>> _instances = [];
    private bool _disposed;

    internal DeclarativeDataTemplate(Func<Element> build)
    {
        ArgumentNullException.ThrowIfNull(build);
        _build = build;
#pragma warning disable CS8305 // Experimental code-based DataTemplate factory, intentionally opt-in.
        Template = new DataTemplate(CreateContent);
#pragma warning restore CS8305
    }

    /// <summary>The stable native template assigned to one or more template properties.</summary>
    public DataTemplate Template { get; }

    private UIElement CreateContent()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var element = _build() ?? throw new InvalidOperationException("A template builder must return an element.");
        Validate(element);
        var reconciler = new Reconciler();
        UIElement? root = null;
        try
        {
            root = reconciler.Mount(element, static () =>
                throw new NotSupportedException("Static template layouts cannot request Reactor re-renders."))
                ?? throw new InvalidOperationException("A template must have a nonempty visual root.");
            var instance = new Instance(reconciler, root);
            root.SetValue(InstanceProperty, instance);
            _instances.RemoveAll(static weak => !weak.TryGetTarget(out _));
            _instances.Add(new(instance));
            return root;
        }
        catch
        {
            try
            {
                if (root is not null) reconciler.UnmountChild(root);
            }
            finally { reconciler.Dispose(); }
            throw;
        }
    }

    private static void Validate(Element element)
    {
        // Deliberately narrow first experiment. No second renderer/mapping table:
        // all supported elements still mount through their ordinary descriptors.
        switch (element)
        {
            case StackElement stack:
                foreach (var child in stack.Children) Validate(child);
                break;
            case GridElement grid:
                foreach (var child in grid.Children) Validate(child);
                break;
            case BorderElement { Child: { } child }:
                Validate(child);
                break;
            case BorderElement or TextBlockElement or Core.IconElement or EmptyElement:
                break;
            default:
                throw new NotSupportedException($"{element.GetType().Name} is outside this static template prototype.");
        }
    }

    /// <summary>
    /// Releases realized trees. Unloaded alone is not disposal: native containers
    /// can reparent or temporarily unload and reload the same template instance.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            foreach (var weak in _instances)
                if (weak.TryGetTarget(out var instance)) instance.Dispose();
        }
        finally { _instances.Clear(); }
    }

    private sealed class Instance(Reconciler reconciler, UIElement root) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { reconciler.UnmountChild(root); }
            finally
            {
                root.ClearValue(InstanceProperty);
                reconciler.Dispose();
            }
        }
    }
}
