#nullable disable
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
        public MainViewModel ViewModel { get; private set; }

        public MainPage()
        {
            this.InitializeComponent();
            
            // Initialize ViewModel asynchronously
            this.Loaded += MainPage_Loaded;
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            var processManager = await ProcessManager.CreateAsync();
            ViewModel = new MainViewModel(processManager);
            this.DataContext = ViewModel;
        }
    }
}
