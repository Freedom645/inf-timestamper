namespace InfTimestamper.Core.Recognition;

public sealed class RoiResource
{
    public IReadOnlyDictionary<string, Roi> Rois { get; init; }
        = new Dictionary<string, Roi>();

    public static RoiResource Empty() => new();

    public bool IsEmpty => Rois.Count == 0;

    public bool TryGet(string key, out Roi roi)
    {
        if (Rois.TryGetValue(key, out var found))
        {
            roi = found;
            return true;
        }
        roi = new Roi(0, 0, 0, 0);
        return false;
    }
}
