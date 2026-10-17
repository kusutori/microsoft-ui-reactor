using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Microsoft.UI.Reactor;

public static partial class ElementExtensions
{
    /// <summary>
    /// Assigns a native WinUI binding through the element's ordinary Set pipeline.
    /// The target is a native dependency property; binding semantics and metadata
    /// requirements are unchanged. Later Set callbacks can override this value.
    /// </summary>
    public static TextBlockElement Bind(this TextBlockElement element, DependencyProperty property, Binding binding)
    {
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(binding);
        return element.Set(text => text.SetBinding(property, binding));
    }

    /// <summary>
    /// Assigns a native binding to an icon's dependency property through Set.
    /// The property must belong to the native icon subtype selected by IconData.
    /// </summary>
    public static Core.IconElement Bind(this Core.IconElement element, DependencyProperty property, Binding binding)
    {
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(binding);
        return element.Set(icon => icon.SetBinding(property, binding));
    }
}
