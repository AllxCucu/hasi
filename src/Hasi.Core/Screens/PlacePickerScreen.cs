using Hasi.Core.Files;
using Hasi.Core.Input;
using Hasi.Core.Output;

namespace Hasi.Core.Screens;

/// <summary>Choosing only a place (Documente, Desktop, Descărcări or a USB stick), for example where to save a copy.</summary>
public sealed class PlacePickerScreen : IScreen
{
    private readonly ListLevel _level;

    /// <param name="onChoose">Runs on Enter on a place.</param>
    public PlacePickerScreen(string title, IFileSystem files, Func<Place, IScreenContext, ScreenResult> onChoose)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(onChoose);
        Title = title;
        _level = new ListLevel(title,
            context => [.. files.Places().Select(place => new MenuItem(PlaceText.Describe(place, context.Localizer),
                c => onChoose(place, c)))],
            context => context.Localizer.Get("files.help.placePicker"),
            "files.noPlaces", "files.help.noPlaces");
    }

    public string Title { get; }

    public Utterance Enter(EnterReason reason, IScreenContext context) => _level.Enter(reason, context);

    public ScreenResult Handle(InputEvent input, IScreenContext context) => _level.Handle(input, context);

    public Utterance Help(IScreenContext context) => _level.Help(context);
}
