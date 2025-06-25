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
        private ProcessViewModel _selectedProcess;

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
                LogsTextBlock.Text = $"Initialization Error:\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}";
                LogsHeaderText.Text = "Application Error";
            }
        }

        private void ProcessListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (e.AddedItems.Count > 0 && e.AddedItems[0] is ProcessViewModel selectedProcess)
                {
                    _selectedProcess = selectedProcess;
                    ShowLogsForProcess(selectedProcess);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ProcessListView_SelectionChanged failed: {ex}");
                LogsTextBlock.Text = $"Error displaying logs: {ex.Message}";
                LogsHeaderText.Text = "Error";
            }
        }

        private void ViewLogsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button button && button.Tag is ProcessViewModel process)
                {
                    _selectedProcess = process;
                    ShowLogsForProcess(process);
                    
                    // Also select the item in the list
                    ProcessListView.SelectedItem = process;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ViewLogsButton_Click failed: {ex}");
                LogsTextBlock.Text = $"Error displaying logs: {ex.Message}";
                LogsHeaderText.Text = "Error";
            }
        }

        private void ShowLogsForProcess(ProcessViewModel process)
        {
            try
            {
                LogsHeaderText.Text = $"Logs for {process.Name}";
                
                if (string.IsNullOrEmpty(process.Logs))
                {
                    LogsTextBlock.Text = $"No logs available for {process.Name}\n\nCurrent Status: {process.Status}";
                }
                else
                {
                    LogsTextBlock.Text = process.Logs;
                }
                
                // Scroll to the bottom to show latest logs
                if (LogsTextBlock.Parent is ScrollViewer scrollViewer)
                {
                    scrollViewer.ChangeView(null, scrollViewer.ScrollableHeight, null);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ShowLogsForProcess failed: {ex}");
                LogsTextBlock.Text = $"Error displaying logs: {ex.Message}";
                LogsHeaderText.Text = "Error";
            }
        }

        private void ClearLogsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedProcess != null)
                {
                    _selectedProcess.Logs = string.Empty;
                    LogsTextBlock.Text = $"Logs cleared for {_selectedProcess.Name}";
                }
                else
                {
                    LogsTextBlock.Text = "No process selected";
                    LogsHeaderText.Text = "Select a process to view logs";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ClearLogsButton_Click failed: {ex}");
                LogsTextBlock.Text = $"Error clearing logs: {ex.Message}";
            }
        }
    }
}
