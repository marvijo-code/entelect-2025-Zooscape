#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ZooscapeRunner.Services;
using ZooscapeRunner.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System.Diagnostics;

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
            try
            {
                Debug.WriteLine("MainPage constructor started");
                
                this.InitializeComponent();
                Debug.WriteLine("InitializeComponent completed");
                
                // Initialize ViewModel immediately with a placeholder
                ViewModel = new MainViewModel(null);
                Debug.WriteLine("ViewModel created");
                
                this.DataContext = ViewModel;
                Debug.WriteLine("DataContext set");
                
                // Load the actual ProcessManager asynchronously
                this.Loaded += MainPage_Loaded;
                Debug.WriteLine("MainPage constructor completed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MainPage constructor failed: {ex}");
                throw;
            }
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Debug.WriteLine("MainPage_Loaded started");
                
                var processManager = await ProcessManager.CreateAsync();
                Debug.WriteLine("ProcessManager created");
                
                // Update the existing ViewModel with the actual ProcessManager
                ViewModel.UpdateProcessManager(processManager);
                Debug.WriteLine("ViewModel updated with ProcessManager");
            }
            catch (Exception ex)
            {
                // Handle initialization errors gracefully
                Debug.WriteLine($"Error initializing ProcessManager: {ex}");
                
                // Update UI to show error state
                ViewModel.AutoRestartText = $"Error: {ex.Message}";
            }
        }
    }
}
