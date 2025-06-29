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
using System.ComponentModel;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;

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
        private bool _isLogsVisible = true;
        private bool _areBotsRunning = false;
        private bool _isVisualizerRunning = false;

        public MainPage()
        {
            try
            {
                Debug.WriteLine("MainPage constructor started");
                Console.WriteLine("MainPage constructor started");
                
                this.InitializeComponent();
                Debug.WriteLine("InitializeComponent completed");
                Console.WriteLine("InitializeComponent completed");
                
                // Initialize ViewModel immediately with a placeholder
                ViewModel = new MainViewModel(null);
                Debug.WriteLine("ViewModel created");
                Console.WriteLine("ViewModel created");
                
                this.DataContext = ViewModel;
                Debug.WriteLine("DataContext set");
                Console.WriteLine("DataContext set");
                
                // Load the actual ProcessManager asynchronously
                this.Loaded += MainPage_Loaded;
                Debug.WriteLine("MainPage constructor completed");
                Console.WriteLine("MainPage constructor completed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MainPage constructor failed: {ex}");
                Console.WriteLine($"MainPage constructor failed: {ex}");
                throw;
            }
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Debug.WriteLine("MainPage_Loaded started");
                Console.WriteLine("MainPage_Loaded started");
                
                var processManager = await ProcessManager.CreateAsync();
                Debug.WriteLine("ProcessManager created");
                Console.WriteLine("ProcessManager created");
                Console.WriteLine($"ProcessManager processes count: {processManager.GetProcesses().Count()}");
                
                // Update the existing ViewModel with the actual ProcessManager
                ViewModel.UpdateProcessManager(processManager);
                Debug.WriteLine("ViewModel updated with ProcessManager");
                Console.WriteLine("ViewModel updated with ProcessManager");
                Console.WriteLine($"ViewModel processes count: {ViewModel.Processes.Count}");
                
                // Update UI with success message
                ViewModel.AutoRestartText = $"Loaded {ViewModel.Processes.Count} processes successfully";
                
                // Show processes in logs with colored text
                var processNames = string.Join(", ", ViewModel.Processes.Select(p => p.Name));
                AddColoredLogText($"🚀 Zooscape Runner Initialized Successfully!\n\n📋 Loaded Processes ({ViewModel.Processes.Count}):\n{processNames}\n\n💡 Quick Start:\n1. Click '▶️ Start All Bots' to begin building and running all processes\n2. Logs will appear here automatically during build and execution\n3. Select any process above to view its specific logs\n4. Use '📊 Start Visualizer' to launch the game visualizer\n\n⏳ Ready to start when you are!", Colors.LightBlue);
                LogsHeaderText.Text = "📱 Application Ready";
                
                // Set up command handlers for toggle buttons with a small delay to ensure UI is loaded
                DispatcherQueue.TryEnqueue(() =>
                {
                    SetupToggleButtons();
                });
            }
            catch (Exception ex)
            {
                // Handle initialization errors gracefully
                Debug.WriteLine($"Error initializing ProcessManager: {ex}");
                Console.WriteLine($"Error initializing ProcessManager: {ex}");
                
                // Update UI to show error state
                ViewModel.AutoRestartText = $"Error: {ex.Message}";
                AddColoredLogText($"Initialization Error:\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}", Colors.Red);
                LogsHeaderText.Text = "❌ Application Error";
            }
        }

        private void SetupToggleButtons()
        {
            // Update button states based on process status
            UpdateButtonStates();
            
            // Subscribe to process status changes
            foreach (var process in ViewModel.Processes)
            {
                process.PropertyChanged += Process_PropertyChanged;
            }
            
            // Subscribe to StartAllBegun event to auto-select process for log viewing
            ViewModel.StartAllBegun += OnStartAllBegun;
        }

        private void Process_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProcessViewModel.Status))
            {
                DispatcherQueue.TryEnqueue(() => UpdateButtonStates());
            }
        }

        private void UpdateButtonStates()
        {
            try
            {
                // Check if any bots are running
                var botProcesses = ViewModel.Processes.Where(p => p.ProcessType != "Visualizer").ToList();
                var visualizerProcesses = ViewModel.Processes.Where(p => p.ProcessType == "Visualizer").ToList();
                
                _areBotsRunning = botProcesses.Any(p => p.Status.Contains("Running") || p.Status.Contains("Started"));
                _isVisualizerRunning = visualizerProcesses.Any(p => p.Status.Contains("Running") || p.Status.Contains("Started"));
                
                // Get styles safely
                Style errorStyle = null;
                Style successStyle = null;
                
                try
                {
                    if (Resources.ContainsKey("ErrorButtonStyle"))
                        errorStyle = (Style)Resources["ErrorButtonStyle"];
                    if (Resources.ContainsKey("SuccessButtonStyle"))
                        successStyle = (Style)Resources["SuccessButtonStyle"];
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Could not access button styles: {ex.Message}");
                }
                
                // Update bot toggle button
                if (_areBotsRunning)
                {
                    ToggleBotsButton.Content = "⏹️ Stop All Bots";
                    if (errorStyle != null)
                        ToggleBotsButton.Style = errorStyle;
                    ToggleBotsButton.Command = ViewModel.StopAllCommand;
                }
                else
                {
                    ToggleBotsButton.Content = "▶️ Start All Bots";
                    if (successStyle != null)
                        ToggleBotsButton.Style = successStyle;
                    ToggleBotsButton.Command = ViewModel.StartAllCommand;
                }
                
                // Update visualizer toggle button
                if (_isVisualizerRunning)
                {
                    ToggleVisualizerButton.Content = "⏹️ Stop Visualizer";
                    if (errorStyle != null)
                        ToggleVisualizerButton.Style = errorStyle;
                    ToggleVisualizerButton.Command = ViewModel.StopVisualizerCommand;
                }
                else
                {
                    ToggleVisualizerButton.Content = "▶️ Start Visualizer";
                    if (successStyle != null)
                        ToggleVisualizerButton.Style = successStyle;
                    ToggleVisualizerButton.Command = ViewModel.StartVisualizerCommand;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateButtonStates failed: {ex}");
                // Fallback - just update content without styles
                if (_areBotsRunning)
                {
                    ToggleBotsButton.Content = "⏹️ Stop All Bots";
                    ToggleBotsButton.Command = ViewModel.StopAllCommand;
                }
                else
                {
                    ToggleBotsButton.Content = "▶️ Start All Bots";
                    ToggleBotsButton.Command = ViewModel.StartAllCommand;
                }
                
                if (_isVisualizerRunning)
                {
                    ToggleVisualizerButton.Content = "⏹️ Stop Visualizer";
                    ToggleVisualizerButton.Command = ViewModel.StopVisualizerCommand;
                }
                else
                {
                    ToggleVisualizerButton.Content = "▶️ Start Visualizer";
                    ToggleVisualizerButton.Command = ViewModel.StartVisualizerCommand;
                }
            }
        }

        private void AddColoredLogText(string text, Windows.UI.Color color)
        {
            try
            {
                LogsRichTextBlock.Blocks.Clear();
                var paragraph = new Paragraph();
                var run = new Run { Text = text };
                run.Foreground = new SolidColorBrush(color);
                paragraph.Inlines.Add(run);
                LogsRichTextBlock.Blocks.Add(paragraph);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AddColoredLogText failed: {ex}");
                // Fallback to simple text
                LogsTextRun.Text = text;
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
                    
                    // Auto-show logs panel when a process is selected
                    if (!_isLogsVisible)
                    {
                        ToggleLogsVisibility();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ProcessListView_SelectionChanged failed: {ex}");
                AddColoredLogText($"Error displaying logs: {ex.Message}", Colors.Red);
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
                    
                    // Auto-show logs panel when view logs is clicked
                    if (!_isLogsVisible)
                    {
                        ToggleLogsVisibility();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ViewLogsButton_Click failed: {ex}");
                AddColoredLogText($"Error displaying logs: {ex.Message}", Colors.Red);
                LogsHeaderText.Text = "Error";
            }
        }

        private void ToggleLogsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ToggleLogsVisibility();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ToggleLogsButton_Click failed: {ex}");
            }
        }

        private void ToggleLogsVisibility()
        {
            _isLogsVisible = !_isLogsVisible;
            
            if (_isLogsVisible)
            {
                // Show logs panel
                LogsPanel.Visibility = Visibility.Visible;
                ToggleLogsButton.Content = "🔽 Hide Logs";
            }
            else
            {
                // Hide logs panel
                LogsPanel.Visibility = Visibility.Collapsed;
                ToggleLogsButton.Content = "🔼 Show Logs";
            }
        }

        private void ShowLogsForProcess(ProcessViewModel process)
        {
            try
            {
                LogsHeaderText.Text = $"📋 Logs for {process.Name}";
                
                if (string.IsNullOrEmpty(process.Logs))
                {
                    var message = $"No logs available for {process.Name}\n\nCurrent Status: {process.Status}\n\nClick 'Start All Bots' to begin building and running processes.\nLogs will appear here in real-time during build and execution.";
                    AddColoredLogText(message, Colors.Gray);
                }
                else
                {
                    // Parse logs and add colors based on content
                    ParseAndDisplayColoredLogs(process.Logs);
                }
                
                // Scroll to the bottom to show latest logs
                if (LogsRichTextBlock.Parent is ScrollViewer scrollViewer)
                {
                    scrollViewer.ChangeView(null, scrollViewer.ScrollableHeight, null);
                }
                
                // Set up property change notification to auto-refresh logs
                if (_selectedProcess != null)
                {
                    _selectedProcess.PropertyChanged -= OnSelectedProcessPropertyChanged;
                }
                _selectedProcess = process;
                _selectedProcess.PropertyChanged += OnSelectedProcessPropertyChanged;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ShowLogsForProcess failed: {ex}");
                AddColoredLogText($"Error displaying logs: {ex.Message}", Colors.Red);
                LogsHeaderText.Text = "❌ Error";
            }
        }

        private void ParseAndDisplayColoredLogs(string logs)
        {
            try
            {
                LogsRichTextBlock.Blocks.Clear();
                var paragraph = new Paragraph();
                
                var lines = logs.Split('\n');
                foreach (var line in lines)
                {
                    var color = GetLogLineColor(line);
                    var run = new Run { Text = line + "\n" };
                    run.Foreground = new SolidColorBrush(color);
                    paragraph.Inlines.Add(run);
                }
                
                LogsRichTextBlock.Blocks.Add(paragraph);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ParseAndDisplayColoredLogs failed: {ex}");
                // Fallback to simple text
                LogsTextRun.Text = logs;
            }
        }

        private Windows.UI.Color GetLogLineColor(string line)
        {
            var lowerLine = line.ToLower();
            
            // Console output colors
            if (lowerLine.Contains("console.writeline") || lowerLine.Contains("debug.writeline"))
                return Colors.Cyan;
            
            // Error colors
            if (lowerLine.Contains("error") || lowerLine.Contains("exception") || lowerLine.Contains("failed"))
                return Colors.Red;
            
            // Warning colors
            if (lowerLine.Contains("warning") || lowerLine.Contains("warn"))
                return Colors.Orange;
            
            // Success colors
            if (lowerLine.Contains("success") || lowerLine.Contains("completed") || lowerLine.Contains("started"))
                return Colors.Green;
            
            // Info colors
            if (lowerLine.Contains("info") || lowerLine.Contains("build") || lowerLine.Contains("restore"))
                return Colors.LightBlue;
            
            // Default color
            return Colors.White;
        }

        private void OnSelectedProcessPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            try
            {
                if (e.PropertyName == nameof(ProcessViewModel.Logs) || e.PropertyName == nameof(ProcessViewModel.Status))
                {
                    if (sender is ProcessViewModel process && process == _selectedProcess)
                    {
                        // Update logs display on UI thread
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            ShowLogsForProcess(process);
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnSelectedProcessPropertyChanged failed: {ex}");
            }
        }

        private void ClearLogsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedProcess != null)
                {
                    _selectedProcess.Logs = string.Empty;
                    AddColoredLogText($"Logs cleared for {_selectedProcess.Name}", Colors.Yellow);
                }
                else
                {
                    AddColoredLogText("No process selected", Colors.Gray);
                    LogsHeaderText.Text = "Select a process to view logs";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ClearLogsButton_Click failed: {ex}");
                AddColoredLogText($"Error clearing logs: {ex.Message}", Colors.Red);
            }
        }

        private async void OpenVisualizerButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string url = "http://localhost:5252";
                Debug.WriteLine($"Opening visualizer in browser: {url}");
                
                // Try to open in default browser
#if WINDOWS
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
#elif ANDROID
                // For Android, use the Android-specific launcher
                var intent = new Android.Content.Intent(Android.Content.Intent.ActionView, Android.Net.Uri.Parse(url));
                intent.AddFlags(Android.Content.ActivityFlags.NewTask);
                Platform.CurrentActivity?.StartActivity(intent);
#elif IOS || MACCATALYST
                // For iOS/macOS, use the system launcher
                await Microsoft.Maui.ApplicationModel.Launcher.OpenAsync(url);
#else
                // Fallback for other platforms
                await Microsoft.Maui.ApplicationModel.Launcher.OpenAsync(url);
#endif
                
                Debug.WriteLine("Browser launched successfully");
                AddColoredLogText($"Opened visualizer in browser: {url}", Colors.Green);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to open browser: {ex}");
                
                // Show user-friendly error message
                string errorMessage = $"Failed to open visualizer in browser.\n\nYou can manually open: http://localhost:5252\n\nError: {ex.Message}";
                AddColoredLogText(errorMessage, Colors.Red);
                LogsHeaderText.Text = "❌ Browser Launch Error";
            }
        }

        private void OnStartAllBegun()
        {
            try
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    // Auto-select the Zooscape Engine process (or first process) for log viewing
                    var engineProcess = ViewModel.Processes.FirstOrDefault(p => p.Name == "Zooscape Engine");
                    var processToSelect = engineProcess ?? ViewModel.Processes.FirstOrDefault();
                    
                    if (processToSelect != null)
                    {
                        Console.WriteLine($"Auto-selecting process for log viewing: {processToSelect.Name}");
                        Debug.WriteLine($"Auto-selecting process for log viewing: {processToSelect.Name}");
                        
                        // Select the process in the ListView
                        ProcessListView.SelectedItem = processToSelect;
                        
                        // Show logs for the selected process
                        ShowLogsForProcess(processToSelect);
                        
                        // Ensure logs panel is visible
                        if (!_isLogsVisible)
                        {
                            ToggleLogsVisibility();
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnStartAllBegun failed: {ex}");
                Console.WriteLine($"OnStartAllBegun failed: {ex}");
            }
        }
    }
}
