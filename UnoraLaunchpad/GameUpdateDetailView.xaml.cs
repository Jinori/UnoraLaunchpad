using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Microsoft.Xaml.Behaviors.Core;

namespace UnoraLaunchpad;

public sealed partial class GameUpdateDetailView
{
    public ICommand CloseCommand { get; private set; }

    public GameUpdateDetailView(GameUpdate gameUpdate)
    {
        InitializeComponent();
        DataContext = gameUpdate;

        CloseCommand = new ActionCommand(Close);
        Loaded += GameUpdateDetailView_Loaded;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void GameUpdateDetailView_Loaded(object sender, RoutedEventArgs e)
    {
        // Create a DoubleAnimation to change the opacity
        var animation = new DoubleAnimation
        {
            From = 0, // Start opacity
            To = 1, // End opacity
            Duration = new Duration(TimeSpan.FromSeconds(0.5)) // Duration of the animation
        };

        // Apply the animation to the window's Opacity property
        BeginAnimation(OpacityProperty, animation);
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }
}