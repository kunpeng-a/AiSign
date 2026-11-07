using System;
using System.Windows;
using AiSign.Core;
using AiSign.Services;
using AiSign.Stores;
using AiSign.UI;

namespace AiSign
{
    internal sealed class AppHost
    {
        private readonly Application _app;
        private readonly bool _startMinimized;
        private readonly bool _uiOnly;

        private MainWindow _window;
        private TrayService _tray;
        private SingleInstance _instance;

        public AppHost(Application app, bool startMinimized, bool uiOnly)
        {
            _app = app;
            _startMinimized = startMinimized;
            _uiOnly = uiOnly;
        }

        public Application App { get { return _app; } }
        public MainWindow Window { get { return _window; } }
        public AppController Controller { get; private set; }

        public void AttachSingleInstance(SingleInstance instance)
        {
            _instance = instance;
            if (instance != null)
            {
                instance.Activated += delegate
                {
                    _app.Dispatcher.BeginInvoke(new Action(ShowWindow));
                };
            }
        }

        public void Start()
        {
            AppPaths.Ensure();
            Logger.Cleanup(7);

            UI.Theme.Install(_app);

            SettingsStore settings = new SettingsStore();
            SecretVault vault = new SecretVault();
            AccountStore accounts = new AccountStore(vault);
            ResultStore results = new ResultStore();
            SchedulerStateStore state = new SchedulerStateStore();
            CheckinCoordinator coordinator = new CheckinCoordinator(accounts, vault, results, state, settings);
            Scheduler scheduler = new Scheduler(settings, accounts, state, coordinator);
            Controller = new AppController(settings, accounts, vault, results, state, scheduler);

            _window = new MainWindow(this, Controller);
            _app.MainWindow = _window;

            if (!_uiOnly)
            {
                _tray = new TrayService(Controller, ShowWindow, ShowCheckinPage, Exit);
                _tray.Create();
                Controller.Start();
            }

            if (!_startMinimized) _window.Show();
            UI.Theme.Apply(settings.Current.Theme);
        }

        public void ShowWindow()
        {
            if (_window == null) return;
            if (!_window.IsVisible) _window.Show();
            if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
            _window.Activate();
            _window.Topmost = true;
            _window.Topmost = false;
        }

        public void ShowCheckinPage()
        {
            if (_window != null) _window.NavigateTo("checkin");
            ShowWindow();
        }

        public void NavigateTo(string page)
        {
            if (_window == null || string.IsNullOrEmpty(page)) return;
            _window.NavigateTo(page);
        }

        public void Exit()
        {
            try { if (_window != null) _window.ForceClose(); } catch { }
            try { if (_tray != null) _tray.Dispose(); } catch { }
            try { if (Controller != null) Controller.Scheduler.Dispose(); } catch { }
            try { if (_instance != null) _instance.Dispose(); } catch { }
            _app.Shutdown();
        }
    }
}
