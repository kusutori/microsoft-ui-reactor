# Static declarative DataTemplate prototype

Local experiment based on `e168195`; not a proposed final API or a release-ready
change. Requires the experimental WinUI code-based DataTemplate constructor
(metapackage 2.5.4-experimental / WinUI 2.3.10-experimental). Other SDK pins,
recipes and release guidance have deliberately not been migrated.

```csharp
using Microsoft.UI.Reactor;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using static Microsoft.UI.Reactor.Factories;
using Xaml = Microsoft.UI.Xaml.Controls;

var owner = Templates.Data(static () =>
    HStack(6,
        Icon(FontIcon("", "Segoe Fluent Icons", 16))
            .Bind(Xaml.FontIcon.GlyphProperty, new Binding
            {
                Path = new PropertyPath("Glyph"),
            }),
        TextBlock("")
            .VAlign(VerticalAlignment.Center)
            .Bind(Xaml.TextBlock.TextProperty, new Binding
            {
                Path = new PropertyPath("Label"),
            })));

// Assign owner.Template to native template properties. Keep owner stable.
// After detaching consumers, call owner.Dispose() on the UI thread.
```

The factory runs once per native template realization. Ordinary Reactor
descriptors mount the Element tree; there is no parallel control-conversion
table and no ReactorHost or render loop per item. WinUI owns DataContext,
bindings and data-object notifications. New realization means new controls,
while a recycled visual keeps its bindings and receives the new DataContext.

`Bind(DependencyProperty, Binding)` currently has overloads for TextBlockElement
and IconElement, the actual use cases in this experiment. Each simply appends a
native SetBinding call to the existing typed Set pipeline. No new binding engine,
data lookup or reflection is introduced. Set/Bind calls retain their order; a
later Set can override the value. The dependency property must match the native
control (including the subtype selected by IconData). This escape hatch retains
Set's lifecycle semantics; removing a Bind from a later declaration is not a
declarative ClearValue contract. Bindable metadata requirements remain unchanged.

This first prototype accepts Stack/Grid/Border layouts, TextBlock, Icon and
Empty children (a nonempty root is required). Other element types are rejected
before mounting. It does not support components, hooks, inherited Reactor
providers, parent-driven render updates or automatic builder replacement.
Use native bindings to change property values, not Reactor re-renders.

The owner tracks live instances weakly; each native root retains its instance
through an attached property, including across managed-wrapper collection.
Consumer-driven disposal unmounts live trees. Unloaded does not dispose:
temporary reparenting or recycling is not proof of permanent destruction.
Callers remain responsible for cleanup of subscriptions made in custom Set
callbacks; this does not invent a cleanup contract for those callbacks.

Tonarink integration: separate `try-reactor-templates` worktree, history file
type picker; the same stable template serves tokens and suggestions. The
existing Label/Glyph bindings and bindable metadata are retained. This branch
does not include the separate ContentDialog theme PR or player Source experiment.
