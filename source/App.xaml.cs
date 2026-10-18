using System.Windows;
using MediaBack.Application;
using MediaBack.Domain;
using MediaBack.Infrastructure;
using MediaBack.Platform;

namespace MediaBack;

public partial class App : System.Windows.Application
{
    private SingleInstance? instance;
    private ApplicationController? controller;
    private SettingsStore? store;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Contains("--self-test-interaction")) { await Diagnostics.RunInteractionOnlyAsync(Argument(e.Args, "--self-test-interaction")); Shutdown(); return; }
        if (e.Args.Contains("--self-test")) { await Diagnostics.RunAsync(Argument(e.Args, "--self-test")); Shutdown(); return; }
        store = new SettingsStore(Argument(e.Args, "--profile"));
        if (e.Args.Contains("--quit")) { SingleInstance.StopExisting(store.DirectoryPath); Shutdown(); return; }
        DispatcherUnhandledException += (_, error) => { ErrorLog.Write(store.DirectoryPath, error.Exception); error.Handled = true; Shutdown(1); };
        try
        {
            instance = new SingleInstance(store.DirectoryPath, () => Dispatcher.BeginInvoke(() => controller?.ShowSettings()), () => Dispatcher.BeginInvoke(() => Shutdown()));
            if (!instance.IsPrimary) { Shutdown(); return; }
            if (!e.Args.Contains("--profile")) store.MigrateLegacyProfile();
            controller = new ApplicationController(store);
            controller.ExitRequested += () => Shutdown();
            var showSettings = e.Args.Contains("--settings") || !e.Args.Contains("--background");
            await controller.StartAsync(showSettings);
        }
        catch (Exception error)
        {
            ErrorLog.Write(store.DirectoryPath, error);
            MessageBox.Show(L.Get("StartupError") + error.Message, "MediaBack", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
    private static string? Argument(string[] args, string key) { var index = Array.IndexOf(args, key); return index >= 0 && index + 1 < args.Length ? args[index + 1] : null; }
    protected override void OnExit(ExitEventArgs e) { controller?.Dispose(); instance?.Dispose(); base.OnExit(e); }
}
