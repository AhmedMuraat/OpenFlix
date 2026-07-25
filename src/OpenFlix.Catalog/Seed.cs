namespace OpenFlix.Catalog;

public static class Seed
{
    public static readonly Media[] Items =
    [
        New("Night of the Living Dead", "Seven strangers fight to survive a terrifying night in a rural farmhouse.",
            "Horror", 1968, "NightOfTheLivingDead", MediaKind.Movie, true, 9421),
        New("His Girl Friday", "A newspaper editor uses every trick he knows to keep his ace reporter—and former wife—from remarrying.",
            "Comedy", 1940, "his_girl_friday", MediaKind.Movie, true, 8110),
        New("The General", "A railway engineer pursues a stolen locomotive through enemy territory.",
            "Comedy", 1926, "TheGeneral1926", MediaKind.Movie, false, 7032),
        New("Sherlock Jr.", "A projectionist dreams himself into the detective story unfolding on screen.",
            "Comedy", 1924, "SherlockJr", MediaKind.Movie, false, 6604),
        New("Charade", "A woman in Paris is pursued by several men who want the fortune her murdered husband stole.",
            "Mystery", 1963, "Charade1963", MediaKind.Movie, true, 6129),
        New("The Phantom Planet", "An astronaut is miniaturized on a mysterious asteroid and pulled into a hidden civilization.",
            "Science Fiction", 1961, "ThePhantomPlanet", MediaKind.Series, true, 5810),
        New("Flash Gordon: Space Soldiers", "Flash Gordon and his allies battle to save Earth in a classic serial adventure.",
            "Adventure", 1936, "FlashGordonSpaceSoldiers", MediaKind.Series, false, 4730),
        New("The Lost World", "Explorers discover a plateau where prehistoric creatures still roam.",
            "Adventure", 1925, "TheLostWorld1925", MediaKind.Series, false, 3920)
    ];

    private static Media New(string title, string synopsis, string genre, int year, string archiveId,
        MediaKind kind, bool featured, long views) => new()
    {
        Title = title, Synopsis = synopsis, Genre = genre, Year = year, Kind = kind, Featured = featured,
        Views = views, MaturityRating = genre == "Horror" ? 16 : 12,
        PosterUrl = $"https://archive.org/services/img/{archiveId}",
        BackdropUrl = $"https://archive.org/services/img/{archiveId}",
        StreamUrl = $"https://archive.org/embed/{archiveId}?autoplay=1",
        SourceUrl = $"https://archive.org/details/{archiveId}",
        License = "Public domain / source-declared open access — verify before production use",
        Attribution = $"Hosted by Internet Archive; source uploader metadata for {title}"
    };
}

