using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows;

namespace UnoraLaunchpad
{
    public partial class SwirlLoader : UserControl
    {
        public SwirlLoader()
        {
            InitializeComponent();
            Loaded += SwirlLoader_Loaded;
            Unloaded += SwirlLoader_Unloaded;
        }

        private void SwirlLoader_Loaded(object sender, RoutedEventArgs e)
        {
            var storyboard = (Storyboard)FindResource("SpinStoryboard");
            storyboard.Begin();
        }

        private void SwirlLoader_Unloaded(object sender, RoutedEventArgs e)
        {
            var storyboard = (Storyboard)FindResource("SpinStoryboard");
            storyboard.Stop();
        }
    }
}