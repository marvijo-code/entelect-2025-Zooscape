using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ZooscapeRunner;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
	[DllImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	static extern bool AllocConsole();

	[DllImport("kernel32.dll")]
	static extern IntPtr GetConsoleWindow();

	[DllImport("user32.dll")]
	static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

	const int SW_SHOW = 5;

	/// <summary>
	/// Initializes the singleton application object.  This is the first line of authored code
	/// executed, and as such is the logical equivalent of main() or WinMain().
	/// </summary>
	public App()
	{
		try
		{
			// Allocate a console for debug output
			AllocConsole();
			var consoleWindow = GetConsoleWindow();
			ShowWindow(consoleWindow, SW_SHOW);
			
			Console.WriteLine("=== ZooscapeRunner Debug Console ===");
			Console.WriteLine("Application starting...");
			
			this.InitializeComponent();
			
			// Add global exception handler
			this.UnhandledException += App_UnhandledException;
			
			Console.WriteLine("App constructor completed successfully");
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"App constructor failed: {ex}");
			Console.WriteLine($"App constructor failed: {ex}");
			throw;
		}
	}

	private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
	{
		Debug.WriteLine($"Unhandled exception: {e.Exception}");
		Console.WriteLine($"Unhandled exception: {e.Exception}");
		// Don't mark as handled so we can see the full error
	}

	/// <summary>
	/// Invoked when the application is launched normally by the end user.
	/// </summary>
	/// <param name="args">Details about the launch request and process.</param>
	protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
	{
		try
		{
			Debug.WriteLine("App.OnLaunched called");
			Console.WriteLine("App.OnLaunched called");
			
			var window = new Window();
			var frame = new Frame();
			
			Debug.WriteLine("Creating MainPage...");
			Console.WriteLine("Creating MainPage...");
			frame.Navigate(typeof(MainPage), args.Arguments);
			
			window.Content = frame;
			Debug.WriteLine("Activating window...");
			Console.WriteLine("Activating window...");
			window.Activate();
			
			Debug.WriteLine("App launched successfully");
			Console.WriteLine("App launched successfully");
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"OnLaunched failed: {ex}");
			Console.WriteLine($"OnLaunched failed: {ex}");
			throw;
		}
	}
}
