using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Diagnostics;

namespace ZooscapeRunner;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
	/// <summary>
	/// Initializes the singleton application object.  This is the first line of authored code
	/// executed, and as such is the logical equivalent of main() or WinMain().
	/// </summary>
	public App()
	{
		try
		{
			this.InitializeComponent();
			
			// Add global exception handler
			this.UnhandledException += App_UnhandledException;
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"App constructor failed: {ex}");
			throw;
		}
	}

	private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
	{
		Debug.WriteLine($"Unhandled exception: {e.Exception}");
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
			
			var window = new Window();
			var frame = new Frame();
			
			Debug.WriteLine("Creating MainPage...");
			frame.Navigate(typeof(MainPage), args.Arguments);
			
			window.Content = frame;
			Debug.WriteLine("Activating window...");
			window.Activate();
			
			Debug.WriteLine("App launched successfully");
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"OnLaunched failed: {ex}");
			throw;
		}
	}
}
