using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace UnoraLaunchpad;

/// <summary>
/// Interaction logic for GameUpdateDetailView.
/// </summary>
public sealed partial class GameUpdateDetailView
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GameUpdateDetailView"/> class.
    /// </summary>
    /// <param name="gameUpdate">The game update to display.</param>
    public GameUpdateDetailView(GameUpdate gameUpdate)
    {
        InitializeComponent();
        DataContext = gameUpdate;
    }

    #region Event Handlers

    /// <summary>
    /// Handles the close button click event.
    /// </summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// Handles the window loaded event and animates opacity.
    /// </summary>
    private void GameUpdateDetailView_Loaded(object sender, RoutedEventArgs e)
    {
        var animation = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = new Duration(TimeSpan.FromSeconds(0.5))
        };
        BeginAnimation(OpacityProperty, animation);
    }

    /// <summary>
    /// Handles dragging the window by the title bar.
    /// </summary>
    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    #endregion
}