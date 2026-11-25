namespace MYG.Electrical.Core.Editing;

/// <summary>Sahne değişikliği. <see cref="ChangedIds"/> null ise tüm sayfa yeniden çizilmelidir.</summary>
public sealed class SceneChangedEventArgs(IReadOnlyCollection<Guid>? changedIds) : EventArgs
{
    public static readonly SceneChangedEventArgs All = new(null);

    public IReadOnlyCollection<Guid>? ChangedIds { get; } = changedIds;
    public bool IsFullRefresh => ChangedIds is null;
}
