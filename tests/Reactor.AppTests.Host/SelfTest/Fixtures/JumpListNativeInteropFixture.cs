using System.Runtime.InteropServices;
using Microsoft.UI.Reactor.Hosting.Shell;

namespace Microsoft.UI.Reactor.AppTests.Host.SelfTest.Fixtures;

/// <summary>Real shell COM calls, also run from the Native AOT self-test executable.</summary>
internal sealed partial class JumpListNativeInteropFixture(Harness harness) : SelfTestFixtureBase(harness)
{
    public override async Task RunAsync()
    {
        // The worker is intentional: source-generated COM does not implicitly
        // initialize the thread-pool apartment as classic COM activation did.
        var probe = await Task.Run(ProbeShellLinks);
        H.Check("JumpListNative_LinkCount", probe.Count == 4, $"count={probe.Count}");
        H.Check("JumpListNative_ArgumentsRoundTrip", probe.Arguments, probe.ArgumentsText);
        H.Check("JumpListNative_ForeignExecutableExcluded", probe.ForeignExcluded);
        H.Check("JumpListNative_TitleWritten", probe.Title == "日本語 title", $"title={probe.Title}");
        H.Check("JumpListNative_SeparatorWritten", probe.Separator == 0xffff, $"separator={probe.Separator}");

        // Only this unique test identity is touched and deleted. Never mutate
        // the user's or another fixture's destination tracking data.
        string testId = "Microsoft.UI.Reactor.JumpListAotTest." + Guid.NewGuid().ToString("N");
        try
        {
            var result = await Task.Run(() =>
            {
                var before = JumpListComInterop.GetRemovedArguments(testId);
                bool first = JumpListComInterop.UpdateUnpackaged(testId,
                    new[] { JumpListItem.ForUri("Native test", "reactor-test://first") }, false, false);
                var between = JumpListComInterop.GetRemovedArguments(testId);
                bool second = JumpListComInterop.UpdateUnpackaged(testId,
                    new[] { JumpListItem.ForUri("Updated test", "reactor-test://second") }, false, false);
                var after = JumpListComInterop.GetRemovedArguments(testId);
                return (first, second, Count: before.Count + between.Count + after.Count);
            });
            H.Check("JumpListNative_CommitAccepted", result.first);
            H.Check("JumpListNative_UpdateAfterReadAccepted", result.second);
            H.Check("JumpListNative_NewIdentityHasNoRemovals", result.Count == 0, $"count={result.Count}");
        }
        finally
        {
            await Task.Run(() => JumpListComInterop.DeleteList(testId));
        }
    }

    private static unsafe (uint Count, bool Arguments, string ArgumentsText, bool ForeignExcluded, string Title, long Separator) ProbeShellLinks()
    {
        using var apartment = JumpListComInterop.InitializeApartment();
        string unicode = "test://设备/日本語?name=\"café\"";
        string longArguments = "open " + new string('x', 2048);
        using var collection = JumpListComInterop.BuildShellLinkArray(new[]
        {
            JumpListItem.ForUri("日本語 title", unicode),
            JumpListItem.ForUri("Long", longArguments),
            JumpListItem.ForUri("Empty", ""),
            new JumpListItem("", "", JumpListItemKind.Separator),
        });
        var array = (JumpListComInterop.IObjectArray)collection.Value;
        Marshal.ThrowExceptionForHR(array.GetCount(out var count));
        string executable = Environment.ProcessPath!;
        var values = JumpListComInterop.ReadRemovedArguments(array, executable.ToUpperInvariant());
        bool roundTrip = values.Count == 3 && values.Contains(unicode) && values.Contains(longArguments) && values.Contains("");
        bool excluded = JumpListComInterop.ReadRemovedArguments(array, executable + ".other.exe").Count == 0;

        string? title = null;
        long separator = 0;
        for (uint i = 0; i < 2; i++)
        {
            var iid = new Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99");
            Marshal.ThrowExceptionForHR(array.GetAt(i == 0 ? 0u : 3u, in iid, out var store));
            try
            {
                var key = i == 0
                    ? new PropertyKey(new Guid("F29F85E0-4FF9-1068-AB91-08002B27B3D9"), 2)
                    : new PropertyKey(new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 6);
                // Independent read-only IPropertyStore ABI, not the setter's projection.
                var getValue = (delegate* unmanaged[Stdcall]<nint, PropertyKey*, PropVariant*, int>)(*(nint**)store)[5];
                PropVariant value = default;
                Marshal.ThrowExceptionForHR(getValue(store, &key, &value));
                try
                {
                    if (i == 0 && value.Type == 31) title = Marshal.PtrToStringUni(value.Data);
                    if (i == 1 && value.Type == 11) separator = value.Data.ToInt64() & 0xffff;
                }
                finally { Marshal.ThrowExceptionForHR(PropVariantClear(ref value)); }
            }
            finally { Marshal.Release(store); }
        }
        return (count, roundTrip, $"distinct={values.Count}", excluded, title ?? "", separator);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PropertyKey(Guid format, uint id)
    {
        public Guid Format = format;
        public uint Id = id;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropVariant
    {
        public ushort Type;
        public ushort Reserved1, Reserved2, Reserved3;
        public nint Data, Data2;
    }

    [LibraryImport("ole32.dll")]
    private static partial int PropVariantClear(ref PropVariant value);
}
