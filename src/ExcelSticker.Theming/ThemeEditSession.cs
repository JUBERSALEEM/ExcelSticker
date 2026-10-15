namespace ExcelSticker.Theming;

public enum ThemeChangeKind { Applied, Preview, Saved, Reverted }
public sealed class ThemeChangedEventArgs(ThemeChangeKind kind, string name, long revision) : EventArgs
{
    public ThemeChangeKind Kind { get; } = kind;
    public string Name { get; } = name;
    public long Revision { get; } = revision;
}

/// <summary>One editor per engine. Save advances the rollback point; failed writes never accept a draft.</summary>
public sealed class ThemeEditSession : IDisposable
{
    private readonly ThemeEngine _engine;
    private ThemeTokens _baseline;
    private bool _disposed;
    internal ThemeEditSession(ThemeEngine engine) { _engine = engine; _baseline = engine.Current; }
    public ThemeTokens Baseline => _baseline.Clone();
    public bool HasSaved { get; private set; }
    public void Preview(ThemeTokens draft) { VerifyOpen(); _engine.ApplyCore(draft, ThemeChangeKind.Preview); }
    public void Save(ThemeTokens draft)
    {
        VerifyOpen();
        // Save validates before touching disk and atomically replaces the destination.
        // An I/O failure leaves the previous file and rollback point intact.
        _engine.Save(draft);
        _baseline = draft.Clone();
        HasSaved = true;
        _engine.ApplyCore(_baseline, ThemeChangeKind.Saved);
    }
    public void Revert() { VerifyOpen(); _engine.ApplyCore(_baseline, HasSaved ? ThemeChangeKind.Saved : ThemeChangeKind.Reverted); }
    private void VerifyOpen() { if (_disposed) throw new ObjectDisposedException(nameof(ThemeEditSession)); }
    public void Dispose()
    {
        if (_disposed) return;
        try { Revert(); }
        finally { _disposed = true; _engine.EndEdit(); }
    }
}
