using Microsoft.UI.Reactor.Navigation;
using Xunit;

namespace Microsoft.UI.Reactor.Tests;

public class ConnectedNavigationHistoryTests
{
    [Fact]
    public void BackAndForwardRememberTheSelectedEdgeNotTheLatestKey()
    {
        var nav = new NavigationHandle<string>(new NavigationStack<string>("list"));
        var host = (INavigationHandle)nav;
        nav.Navigate("detail", new() { Transition = NavigationTransition.Connected("first") });
        nav.Navigate("child", new() { Transition = NavigationTransition.Connected("second") });
        host.PendingTransitionOverride = null;
        Assert.True(nav.GoBack());
        Assert.Equal("second", Assert.IsType<ConnectedTransition>(host.PendingTransitionOverride).AnimationKey);
        Assert.True(nav.GoBack());
        Assert.Equal("first", Assert.IsType<ConnectedTransition>(host.PendingTransitionOverride).AnimationKey);
        Assert.True(nav.GoForward());
        Assert.Equal("first", Assert.IsType<ConnectedTransition>(host.PendingTransitionOverride).AnimationKey);
        Assert.True(nav.GoForward());
        Assert.Equal("second", Assert.IsType<ConnectedTransition>(host.PendingTransitionOverride).AnimationKey);
    }

    [Fact]
    public void CancelledNavigationDoesNotChangeReturnAnimation()
    {
        var stack = new NavigationStack<string>("list");
        var nav = new NavigationHandle<string>(stack);
        nav.Navigate("detail", new() { Transition = NavigationTransition.Connected("hero") });
        stack.Guard = context => context.Cancel();
        Assert.False(nav.Navigate("other", new() { Transition = NavigationTransition.Connected("wrong") }));
        Assert.False(nav.GoBack());
        stack.Guard = null;
        Assert.True(nav.GoBack());
        Assert.Equal("hero", Assert.IsType<ConnectedTransition>(((INavigationHandle)nav).PendingTransitionOverride).AnimationKey);
    }

    [Fact]
    public void RestoredStateAndReplacementDoNotReuseStaleEdges()
    {
        var nav = new NavigationHandle<string>(new NavigationStack<string>("list"));
        var host = (INavigationHandle)nav;
        nav.Navigate("detail", new() { Transition = NavigationTransition.Connected("hero") });
        nav.Replace("unrelated");
        nav.GoBack();
        Assert.Null(host.PendingTransitionOverride);
        nav.SetState(new(["a", "b"], "c", ["d"]));
        Assert.True(nav.GoBack());
        Assert.Null(host.PendingTransitionOverride);
        Assert.True(nav.GoForward());
        Assert.Null(host.PendingTransitionOverride);
        Assert.True(nav.GoForward());
        Assert.Null(host.PendingTransitionOverride);
    }

    [Fact]
    public void MultiPopAndNewPushKeepHistoryAligned()
    {
        var nav = new NavigationHandle<string>(new NavigationStack<string>("a"));
        var host = (INavigationHandle)nav;
        nav.Navigate("b", new() { Transition = NavigationTransition.Connected("ab") });
        nav.Navigate("c", new() { Transition = NavigationTransition.Connected("bc") });
        Assert.True(nav.PopTo(route => route == "a"));
        Assert.Null(host.PendingTransitionOverride);
        Assert.True(nav.GoForward());
        Assert.Equal("ab", Assert.IsType<ConnectedTransition>(host.PendingTransitionOverride).AnimationKey);
        nav.Navigate("new");
        Assert.False(nav.CanGoForward);
        Assert.True(nav.GoBack());
        Assert.Null(host.PendingTransitionOverride);
    }
}
