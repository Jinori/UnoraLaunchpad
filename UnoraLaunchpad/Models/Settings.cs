namespace UnoraLaunchpad.Models; // Updated namespace

public sealed class Settings
{
    public bool SkipIntro { get; set; }
    public bool UseDawndWindower { get; set; }
    public bool UseLocalhost { get; set; }
    public string SelectedTheme { get; set; }
    public string SelectedGame { get; set; }
    public double WindowHeight { get; set; }
    public double WindowWidth { get; set; }
    public double WindowTop { get; set; }
    public double WindowLeft { get; set; }

    // Adding the default and copy constructors that were previously attempted during documentation
    // to ensure the class remains functional as it was before documentation was reverted.
    public Settings()
    {
        SkipIntro = false;
        UseDawndWindower = false;
        UseLocalhost = false;
        SelectedTheme = "Dark";
        SelectedGame = "Unora";
        WindowHeight = 600;
        WindowWidth = 900;
        WindowTop = 0;
        WindowLeft = 0;
    }

    public Settings(Settings other)
    {
        if (other == null) return;

        SkipIntro = other.SkipIntro;
        UseDawndWindower = other.UseDawndWindower;
        UseLocalhost = other.UseLocalhost;
        SelectedTheme = other.SelectedTheme;
        SelectedGame = other.SelectedGame;
        WindowHeight = other.WindowHeight;
        WindowWidth = other.WindowWidth;
        WindowTop = other.WindowTop;
        WindowLeft = other.WindowLeft;
    }
}
