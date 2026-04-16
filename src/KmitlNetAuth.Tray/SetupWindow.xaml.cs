using System.ComponentModel;
using System.Runtime.Versioning;
using System.Windows;
using KmitlNetAuth.Core;
using KmitlNetAuth.Core.Platform;
using KmitlNetAuth.Tray.Pages.Setup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;

namespace KmitlNetAuth.Tray;

/// <summary>
/// First-run setup wizard. Walks the user through a Welcome -&gt; Credentials
/// -&gt; Confirm flow and persists the collected values via
/// <see cref="Config.Save(string, ICredentialStore?, ILogger?)"/>.
/// </summary>
[SupportedOSPlatform("windows10.0.17763.0")]
public partial class SetupWindow : FluentWindow
{
    private enum Step
    {
        Welcome = 0,
        Credentials = 1,
        Confirm = 2,
    }

    private readonly Config _config;
    private readonly string _configPath;
    private readonly ICredentialStore? _credentialStore;
    private readonly ILogger<SetupWindow>? _logger;

    private readonly WelcomePage _welcomePage;
    private readonly CredentialsPage _credentialsPage;
    private readonly ConfirmPage _confirmPage;

    private Step _currentStep = Step.Welcome;

    /// <summary>
    /// Set to true once the user explicitly completes or cancels the wizard
    /// via the footer buttons. Used by <see cref="OnClosing"/> to distinguish
    /// an intentional close (where <see cref="Window.DialogResult"/> has
    /// already been set) from a clicked X-button (where it has not).
    /// </summary>
    private bool _userClosed;

    public SetupWindow(IServiceProvider services, string configPath)
    {
        // Match wpfui guidance: subscribe to system theme before XAML loads.
        SystemThemeWatcher.Watch(this);

        InitializeComponent();

        _config = services.GetRequiredService<Config>();
        _configPath = configPath;
        _credentialStore = services.GetService<ICredentialStore>();
        _logger = services.GetService<ILogger<SetupWindow>>();

        _welcomePage = new WelcomePage();
        _credentialsPage = new CredentialsPage();
        _confirmPage = new ConfirmPage();

        Loaded += (_, _) => ShowStep(Step.Welcome);
    }

    // -----------------------------------------------------------------
    // Navigation
    // -----------------------------------------------------------------

    private void ShowStep(Step step)
    {
        _currentStep = step;

        switch (step)
        {
            case Step.Welcome:
                ContentFrame.Navigate(_welcomePage);
                break;
            case Step.Credentials:
                ContentFrame.Navigate(_credentialsPage);
                // Defer focus until the frame renders the page.
                Dispatcher.BeginInvoke(new Action(_credentialsPage.FocusFirstField));
                break;
            case Step.Confirm:
                _confirmPage.SetSummary(
                    _credentialsPage.Username,
                    _credentialsPage.Password,
                    _credentialsPage.IpAddress);
                ContentFrame.Navigate(_confirmPage);
                break;
        }

        UpdateChrome();
    }

    /// <summary>Refresh footer labels/buttons based on current step.</summary>
    private void UpdateChrome()
    {
        var stepNumber = (int)_currentStep + 1;
        StepLabel.Text = $"Step {stepNumber} of 3";

        BackButton.IsEnabled = _currentStep != Step.Welcome;
        NextButton.Content = _currentStep == Step.Confirm ? "Finish" : "Next";
    }

    // -----------------------------------------------------------------
    // Button handlers
    // -----------------------------------------------------------------

    private void OnBackClicked(object sender, RoutedEventArgs e)
    {
        if (_currentStep == Step.Welcome)
            return;

        ShowStep(_currentStep - 1);
    }

    private void OnNextClicked(object sender, RoutedEventArgs e)
    {
        switch (_currentStep)
        {
            case Step.Welcome:
                ShowStep(Step.Credentials);
                break;

            case Step.Credentials:
                if (!_credentialsPage.Validate())
                    return;
                ShowStep(Step.Confirm);
                break;

            case Step.Confirm:
                Finish();
                break;
        }
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        CloseWithResult(false);
    }

    // -----------------------------------------------------------------
    // Finish / close plumbing
    // -----------------------------------------------------------------

    private void Finish()
    {
        // Persist to the DI Config singleton; MainWindow will observe the
        // same instance and pick up these values on construction.
        _config.Username = _credentialsPage.Username;
        _config.Password = _credentialsPage.Password;
        _config.IpAddress = _credentialsPage.IpAddress;

        try
        {
            _config.Save(_configPath, _credentialStore);
            _logger?.LogInformation(
                "Setup wizard completed for user {Username}",
                _config.Username);
        }
        catch (Exception ex)
        {
            // Log, but still allow the window to close; the next launch will
            // drop back into the wizard because Username persists in memory
            // only, not on disk.
            _logger?.LogError(ex, "Failed to save configuration from setup wizard");
            Serilog.Log.Error(ex, "Failed to save configuration from setup wizard");
            MessageBox.Show(
                this,
                $"Could not save your settings:\n\n{ex.Message}",
                "Setup - KMITL NetAuth",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        CloseWithResult(true);
    }

    private void CloseWithResult(bool result)
    {
        _userClosed = true;
        DialogResult = result;
        Close();
    }

    /// <summary>
    /// If the user clicks the X in the title bar we still want the wizard
    /// to be cancellable, so treat it identically to Cancel (DialogResult =
    /// false) and let the window close.
    /// </summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_userClosed)
        {
            // Closing via X button or Alt+F4. Ensure DialogResult is set so
            // ShowDialog() returns false, not null.
            _userClosed = true;
            DialogResult = false;
        }

        base.OnClosing(e);
    }
}
