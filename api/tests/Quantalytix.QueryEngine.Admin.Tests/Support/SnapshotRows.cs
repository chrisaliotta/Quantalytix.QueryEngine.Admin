using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Tests.Support;

/// <summary>Builds the snapshot rows the tests seed: the live snapshot every fixture edits, and a frozen one.</summary>
internal static class SnapshotRows
{
    /// <summary>The live snapshot's id.</summary>
    public const long LiveId = 1;

    /// <summary>The frozen snapshot's id.</summary>
    public const long FrozenId = 2;

    /// <summary>The live snapshot.</summary>
    public static SnapshotRecord Live()
    {
        return new SnapshotRecord { SnapshotId = LiveId, SnapshotKey = "live", DisplayName = "Live", IsLive = true, CreatedAt = DateTime.UnixEpoch };
    }

    /// <summary>A frozen snapshot.</summary>
    public static SnapshotRecord Frozen(long snapshotId = FrozenId, string key = "fy2025_close")
    {
        return new SnapshotRecord
        {
            SnapshotId = snapshotId,
            SnapshotKey = key,
            DisplayName = key,
            IsLive = false,
            CapturedAt = DateTime.UnixEpoch,
            CapturedBy = "tester",
            CreatedAt = DateTime.UnixEpoch
        };
    }
}
