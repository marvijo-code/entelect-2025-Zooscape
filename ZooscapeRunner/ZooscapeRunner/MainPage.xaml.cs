using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ZooscapeRunner.Services;
using ZooscapeRunner.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;

#if WINDOWS
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
#endif

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace ZooscapeRunner
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainViewModel? ViewModel { get; private set; }

        public MainPage()
        {
            // Create UI programmatically since we're not using XAML
            InitializeUI();
            Loaded += MainPage_Loaded;
        }

        private void InitializeUI()
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var titleBlock = new TextBlock
            {
                Text = "Zooscape Runner",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(20)
            };
            Grid.SetRow(titleBlock, 0);

            var contentPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(20)
            };

            var statusText = new TextBlock
            {
                Text = "Ready to start Zooscape processes",
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 20)
            };

            var startButton = new Button
            {
                Content = "Start Processes",
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(10)
            };

            contentPanel.Children.Add(statusText);
            contentPanel.Children.Add(startButton);
            Grid.SetRow(contentPanel, 1);

            grid.Children.Add(titleBlock);
            grid.Children.Add(contentPanel);

            Content = grid;
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            var processManager = await Services.ProcessManager.CreateAsync();
            ViewModel = new MainViewModel(processManager);
            DataContext = ViewModel;
        }
    }
}
