﻿namespace UnoraLaunchpad;

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
}