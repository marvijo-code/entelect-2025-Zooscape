extern alias UnoSdk;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ZooscapeRunner.Services;
using ZooscapeRunner.ViewModels;

#if WINDOWS
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Page = UnoSdk::Microsoft.UI.Xaml.Controls.Page;
using RoutedEventArgs = UnoSdk::Microsoft.UI.Xaml.RoutedEventArgs;
using Grid = UnoSdk::Microsoft.UI.Xaml.Controls.Grid;
using TextBlock = UnoSdk::Microsoft.UI.Xaml.Controls.TextBlock;
using Button = UnoSdk::Microsoft.UI.Xaml.Controls.Button;
using StackPanel = UnoSdk::Microsoft.UI.Xaml.Controls.StackPanel;
using RowDefinition = UnoSdk::Microsoft.UI.Xaml.Controls.RowDefinition;
using GridLength = UnoSdk::Microsoft.UI.Xaml.GridLength;
using GridUnitType = UnoSdk::Microsoft.UI.Xaml.GridUnitType;
using HorizontalAlignment = UnoSdk::Microsoft.UI.Xaml.HorizontalAlignment;
using VerticalAlignment = UnoSdk::Microsoft.UI.Xaml.VerticalAlignment;
using Thickness = UnoSdk::Microsoft.UI.Xaml.Thickness;
using Orientation = UnoSdk::Microsoft.UI.Xaml.Controls.Orientation;
#else
using Page = UnoSdk::Microsoft.UI.Xaml.Controls.Page;
using RoutedEventArgs = UnoSdk::Microsoft.UI.Xaml.RoutedEventArgs;
using Grid = UnoSdk::Microsoft.UI.Xaml.Controls.Grid;
using TextBlock = UnoSdk::Microsoft.UI.Xaml.Controls.TextBlock;
using Button = UnoSdk::Microsoft.UI.Xaml.Controls.Button;
using StackPanel = UnoSdk::Microsoft.UI.Xaml.Controls.StackPanel;
using RowDefinition = UnoSdk::Microsoft.UI.Xaml.Controls.RowDefinition;
using GridLength = UnoSdk::Microsoft.UI.Xaml.GridLength;
using GridUnitType = UnoSdk::Microsoft.UI.Xaml.GridUnitType;
using HorizontalAlignment = UnoSdk::Microsoft.UI.Xaml.HorizontalAlignment;
using VerticalAlignment = UnoSdk::Microsoft.UI.Xaml.VerticalAlignment;
using Thickness = UnoSdk::Microsoft.UI.Xaml.Thickness;
using Orientation = UnoSdk::Microsoft.UI.Xaml.Controls.Orientation;
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
            this.InitializeComponent();
            Loaded += MainPage_Loaded;
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            var processManager = await Services.ProcessManager.CreateAsync();
            ViewModel = new MainViewModel(processManager);
            DataContext = ViewModel;
        }
    }
}
