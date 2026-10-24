using Hasi.Core.Localization;

namespace Hasi.Core.Files;

/// <summary>How places and free space are said: "Documente", "Stick USB, Kingston, 15 gigaocteți liberi".</summary>
public static class PlaceText
{
    private const long Megabyte = 1024L * 1024;
    private const long Gigabyte = 1024L * Megabyte;

    /// <summary>The short name, used as a title: "Descărcări", "Stick USB, Kingston".</summary>
    public static string Name(Place place, ILocalizer localizer) => place.Kind switch
    {
        PlaceKind.Documents => localizer.Get("files.place.documents"),
        PlaceKind.Desktop => localizer.Get("files.place.desktop"),
        PlaceKind.Downloads => localizer.Get("files.place.downloads"),
        _ => string.IsNullOrWhiteSpace(place.Label)
            ? localizer.Get("files.place.stick")
            : localizer.Get("files.place.stickLabel", ("label", place.Label)),
    };

    /// <summary>The name with the free space, when known: "Stick USB, Kingston, 15 gigaocteți liberi".</summary>
    public static string Describe(Place place, ILocalizer localizer)
    {
        var name = Name(place, localizer);
        return place.FreeBytes is { } free
            ? localizer.Get("files.place.withFree", ("place", name), ("free", FreeSpace(free, localizer)))
            : name;
    }

    /// <summary>Whole gigabytes from 1 GB up ("15 gigaocteți liberi"), else whole megabytes ("800 de megaocteți liberi"); rounded down.</summary>
    public static string FreeSpace(long bytes, ILocalizer localizer)
    {
        bytes = Math.Max(0, bytes);
        return bytes >= Gigabyte
            ? localizer.Plural("files.free.gigabytes", bytes / Gigabyte)
            : localizer.Plural("files.free.megabytes", bytes / Megabyte);
    }
}
