using JacobC.Xiami.Services;
using JacobC.Xiami.Net;
using System.Threading.Tasks;
using Template10.Common;
using Template10.Controls;
using Windows.ApplicationModel.Activation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using Windows.ApplicationModel;

namespace JacobC.Xiami
{
    /// Documentation on APIs used in this page:
    /// https://github.com/Windows-XAML/Template10/wiki

    [Bindable]
    sealed partial class App : BootStrapper
    {
        public App()
        {
            InitializeComponent();
            SplashFactory = (e) => new Views.Splash(e);

            #region App settings

            var _settings = SettingsService.Instance;
            RequestedTheme = _settings.AppTheme;
            CacheMaxDuration = _settings.CacheMaxDuration;
            ShowShellBackButton = _settings.UseShellBackButton;

            #endregion

            UnhandledException += App_UnhandledException;
        }

        private void App_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Services.LogService.ErrorWrite(e.Exception, sender.ToString());
        }

        public override async Task OnInitializeAsync(IActivatedEventArgs args)
        {

            if (Window.Current.Content as ModalDialog == null)
            {
                // create a new frame 
                var nav = NavigationServiceFactory(BackButton.Attach, ExistingContent.Include);

                // create modal root
                Window.Current.Content = new ModalDialog
                {
                    DisableBackButtonWhenModal = true,
                    Content = new Views.Shell(nav),
                    ModalContent = new Views.Busy(),
                };
            }
            await Task.CompletedTask;
        }

        public override async Task OnStartAsync(StartKind startKind, IActivatedEventArgs args)
        {
            // long-running startup tasks go here
            //await Task.Delay(5000);

            //Custom code segment
            SetTitleColor();
            PlaybackService.Instance.StartBackgroundAudioTask();

            NavigationService.Navigate(typeof(Views.DiscoveryPage));
            await Task.CompletedTask;
        }
         
        public override Task OnPrelaunchAsync(IActivatedEventArgs args, out bool runOnStartAsync)
        {
            PlaybackService.Instance.StartBackgroundAudioTask();
            runOnStartAsync = true;
            return Task.CompletedTask;
        }

        public override async Task OnSuspendingAsync(object s, SuspendingEventArgs e, bool prelaunchActivated)
        {
            HttpHelper.SaveCookies();
            LoginHelper.SaveAccountInfo();
            await Task.CompletedTask;
        }

        /// <summary>
        /// Set the style of the window title bar
        /// <!--This code is custom-->
        /// </summary>
        private void SetTitleColor()
        {
            var titlebar = Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().TitleBar;
            Color theme = (Color)(Resources["ThemeColor"]);
            Color inactive = (Color)(Resources["InactiveThemeColor"]);
            Color hover = (Color)(Resources["HighlightThemeColor"]);
            //Active color
            titlebar.BackgroundColor = theme;
            titlebar.ForegroundColor = Colors.White;
            titlebar.ButtonBackgroundColor = theme;
            titlebar.ButtonForegroundColor = Colors.White;
            //Inactive color
            titlebar.InactiveBackgroundColor = inactive;
            titlebar.InactiveForegroundColor = Colors.White;
            titlebar.ButtonInactiveBackgroundColor = inactive;
            titlebar.ButtonInactiveForegroundColor = Colors.White;
            //Event color
            titlebar.ButtonHoverBackgroundColor = hover;
            titlebar.ButtonHoverForegroundColor = Colors.White;
            titlebar.ButtonPressedBackgroundColor = inactive;
        }
    }
}
