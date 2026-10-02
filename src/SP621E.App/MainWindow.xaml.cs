using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using SP621E.Audio.Capture;
using SP621E.Bluetooth;
using SP621E.Bluetooth.Snoop;
using SP621E.Bluetooth.Transport;
using SP621E.Core.Audio.Analysis;
using SP621E.Core.Audio.BeatDetection;
using SP621E.Core.Audio.Capture;
using SP621E.Core.Configuration;
using SP621E.Core.Controllers;
using SP621E.Core.Effects;
using SP621E.Core.LedFrame;
using SP621E.Core.Logging;
using SP621E.Screen;

namespace SP621E.App;

public partial class MainWindow : Window
{
    private readonly List<string> _logBuffer = [];
    private readonly object _logLock = new();

    // Advanced-tab forensics.
    private readonly BleDeviceScanner _scanner = new();
    private readonly ObservableCollection<BleDeviceInfo> _devices = [];
    private BleDeviceInfo? _selectedDevice;
    private GattReport? _currentReport;
    private string? _currentSnoopText;
    private string? _currentReportPath;

    // Connection tab.
    private readonly BleDeviceScanner _connScanner = new();
    private readonly ObservableCollection<BleDeviceInfo> _connDevices = [];
    private BleDeviceInfo? _connSelected;
    private IBleTransport? _transport;
    private SP621EDriver? _driver;

    // Settings.
    private readonly SettingsStore _settingsStore = new();
    private AppSettings _settings;

    // Effects pipeline.
    private readonly EffectsEngine _engine;
    private readonly ObservableCollection<SolidColorBrush> _previewBrushes = [];
    private readonly DispatcherTimer _renderTimer;
    private readonly Stopwatch _clock = new();
    private int _frameSendFailuresLogged;

    // Audio pipeline.
    private IAudioSource? _audioSource;
    private AudioAnalyzer? _analyzer;
    private BeatDetector? _beatDetector;
    private AudioData? _latestAudio;
    private IReadOnlyList<AudioDeviceInfo> _audioDevices = [];

    // Screen sync pipeline.
    private readonly ScreenSyncPipeline _screenPipeline;
    private readonly ObservableCollection<SolidColorBrush> _screenPaletteBrushes = [];
    private readonly DispatcherTimer _screenTimer;
    private ScreenSampler? _screenSampler;
    private ScreenSnapshot? _screenLatest;
    private bool _screenCaptureInFlight;
    private bool _screenCaptureErrorLogged;
    private bool _renderAllDark;

    private bool _ready;

    public MainWindow()
    {
        _settings = _settingsStore.Load();
        _engine = new EffectsEngine(Math.Max(1, _settings.LedCount));
        _screenPipeline = new ScreenSyncPipeline(_engine.PixelCount);

        InitializeComponent();

        _renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _renderTimer.Tick += RenderTimer_Tick;

        _screenTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _screenTimer.Tick += ScreenTimer_Tick;

        DevicesList.ItemsSource = _devices;
        AppLog.EntryWritten += OnLogEntry;
        _scanner.DeviceDiscovered += OnDeviceDiscovered;
        _scanner.ScanFailed += OnScanFailed;
        _connScanner.DeviceDiscovered += OnConnDeviceDiscovered;
        _connScanner.ScanFailed += OnConnScanFailed;

        ConnDeviceList.ItemsSource = _connDevices;
        AudioDeviceCombo.ItemsSource = _audioDevices;
        EffectCombo.ItemsSource = _engine.Registry.Effects;

        ScreenModeCombo.ItemsSource = new List<ScreenModeOption>
        {
            new(ScreenSyncMode.Average, "Average (whole screen)"),
            new(ScreenSyncMode.BottomEdge, "Bottom edge columns"),
            new(ScreenSyncMode.Columns, "Full-screen columns"),
        };
        InitializeScreenPalette(8);
        ScreenPalettePreview.ItemsSource = _screenPaletteBrushes;

        InitializePreview(_engine.PixelCount);
        PixelsText.Text = $"LED strip: {_engine.PixelCount} pixels";

        ApplySettings();
        _ready = true;
        if (_settings.ScreenSyncEnabled)
            ScreenEnableCheck.IsChecked = true;
        AppLog.Info("Shell started; settings loaded.");
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _renderTimer.Stop();
        _screenTimer.Stop();
        StopAudioSource();
        var driver = _driver;
        if (driver is not null)
        {
            try { _ = driver.DisconnectAsync(); } catch { }
        }
        SaveSettings();
        _connScanner.Dispose();
        _scanner.Dispose();
        base.OnClosing(e);
    }

    private void ApplySettings()
    {
        _engine.GlobalBrightness = Math.Clamp(_settings.LastBrightness, 0.0, 1.0);
        _engine.Intensity = 1.0;
        _engine.Speed = 1.0;

        RadioSourceInput.IsChecked = _settings.IsInputAudioSource;
        if (!_settings.IsInputAudioSource)
            RadioSourceLoopback.IsChecked = true;

        BrightnessSlider.Value = _engine.GlobalBrightness;
        IntensitySlider.Value = _engine.Intensity;
        SpeedSlider.Value = _engine.Speed;

        if (!string.IsNullOrEmpty(_settings.LastEffectName))
        {
            var match = _engine.Registry.Effects.FirstOrDefault(f => f.Name == _settings.LastEffectName);
            if (match is not null)
                EffectCombo.SelectedItem = match;
        }

        ScreenSmoothSlider.Value = Math.Clamp(_settings.ScreenSyncSmoothness, 0.0, 1.0);
        ScreenBrightSlider.Value = Math.Clamp(_settings.ScreenSyncBrightness, 0.0, 1.0);
        UpdateSliderValueLabels();
        if (Enum.TryParse<ScreenSyncMode>(_settings.ScreenSyncMode, out var savedMode))
        {
            foreach (var item in ScreenModeCombo.Items)
            {
                if (item is ScreenModeOption { Mode: var mode } && mode == savedMode)
                {
                    ScreenModeCombo.SelectedItem = item;
                    break;
                }
            }
        }
    }

    private void SaveSettings()
    {
        _settings.LedCount = _engine.PixelCount;
        _settings.LastBrightness = _engine.GlobalBrightness;
        _settings.LastEffectName = ActiveEffectName;
        _settings.ScreenSyncEnabled = ScreenEnableCheck.IsChecked == true;
        _settings.ScreenSyncMode = CurrentScreenMode().ToString();
        _settings.ScreenSyncSmoothness = ScreenSmoothSlider.Value;
        _settings.ScreenSyncBrightness = ScreenBrightSlider.Value;
        if (AudioDeviceCombo.SelectedItem is AudioDeviceInfo device)
            _settings.LastAudioDeviceId = device.Id;
        _settings.IsInputAudioSource = RadioSourceInput.IsChecked == true;
        if (_connSelected is not null)
        {
            _settings.LastDeviceName = _connSelected.Name;
            _settings.LastDeviceAddress = _connSelected.Address;
        }
        _settingsStore.Save(_settings);
    }

    private string? ActiveEffectName => _engine.Registry.ActiveEffect?.Name;

    private void UpdateSliderValueLabels()
    {
        BrightnessValueText.Text = $"{Math.Round(_engine.GlobalBrightness * 100)}%";
        ScreenSmoothValueText.Text = ScreenSmoothSlider.Value.ToString("F2");
        ScreenBrightValueText.Text = $"{Math.Round(ScreenBrightSlider.Value * 100)}%";
    }

    // ------------------------------------------------------------------
    // Log
    // ------------------------------------------------------------------

    private void OnLogEntry(string line)
    {
        Dispatcher.BeginInvoke(() =>
        {
            lock (_logLock)
            {
                _logBuffer.Add(line);
                if (_logBuffer.Count > 500)
                    _logBuffer.RemoveRange(0, _logBuffer.Count - 500);
                LogView.Text = string.Join(Environment.NewLine, _logBuffer);
                LogView.ScrollToEnd();
            }
        });
    }

    // ------------------------------------------------------------------
    // Advanced tab (BLE forensics)
    // ------------------------------------------------------------------

    private void OnDeviceDiscovered(object? sender, BleDeviceDiscoveredEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            var existing = _devices.FirstOrDefault(d => d.AddressValue == e.Device.AddressValue);
            if (existing is null)
                _devices.Add(e.Device);
            else
                _devices[_devices.IndexOf(existing)] = e.Device;
            ScanStatus.Text = $"Scanning... devices found: {_devices.Count}";
        });
    }

    private void OnScanFailed(object? sender, string message)
    {
        Dispatcher.BeginInvoke(() =>
        {
            DiagStatus.Text = message;
            AppLog.Error(message);
        });
    }

    private void BtnScan_Click(object sender, RoutedEventArgs e)
    {
        _devices.Clear();
        _selectedDevice = null;
        BtnEnumerateGatt.IsEnabled = false;
        DiagStatus.Text = string.Empty;
        AppLog.Info("Starting BLE advertisement scan.");

        try
        {
            _scanner.Start();
            BtnScan.IsEnabled = false;
            BtnStopScan.IsEnabled = true;
        }
        catch (Exception ex)
        {
            DiagStatus.Text = $"Scan could not start: {ex.Message}";
            AppLog.Error("BLE scan failed to start", ex);
        }
    }

    private void BtnStopScan_Click(object sender, RoutedEventArgs e)
    {
        _scanner.Stop();
        BtnScan.IsEnabled = true;
        BtnStopScan.IsEnabled = false;
        ScanStatus.Text = $"Scan stopped. Devices found: {_devices.Count}";
        AppLog.Info("BLE advertisement scan stopped.");
    }

    private void DevicesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedDevice = DevicesList.SelectedItem as BleDeviceInfo;
        BtnEnumerateGatt.IsEnabled = _selectedDevice is not null;
    }

    private async void BtnEnumerateGatt_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedDevice is null)
        {
            DiagStatus.Text = "Select a device first.";
            return;
        }

        BtnEnumerateGatt.IsEnabled = false;
        GattReportText.Text = "Connecting and enumerating GATT... (this can take several seconds)";
        DiagStatus.Text = string.Empty;
        AppLog.Info($"Enumerating GATT for {_selectedDevice.Name} ({_selectedDevice.Address}).");

        try
        {
            var inspector = new BleGattInspector();
            var report = await inspector.InspectAsync(_selectedDevice.AddressValue);

            _currentReport = report;
            _currentSnoopText = null;
            GattReportText.Text = report.ToString();

            _currentReportPath = DiagnosticReportStore.SaveAndReturnPath(report);
            ReportPathText.Text = _currentReportPath;
            BtnSaveReport.IsEnabled = true;

            AppLog.Info($"GATT enumeration complete; report saved to {_currentReportPath}");
            DiagStatus.Text = "Enumeration complete. This report is UNVERIFIED until a human confirms it matches the real SP621E.";
        }
        catch (Exception ex)
        {
            DiagStatus.Text = $"Enumeration failed: {ex.Message}";
            AppLog.Error("GATT enumeration failed", ex);
        }
        finally
        {
            BtnEnumerateGatt.IsEnabled = _selectedDevice is not null;
        }
    }

    private void BtnSaveReport_Click(object sender, RoutedEventArgs e)
    {
        if (_currentReport is not null)
        {
            DiagnosticReportStore.Save(_currentReport, _currentReportPath);
            DiagStatus.Text = "Report saved.";
            AppLog.Info($"Report re-saved to {_currentReportPath}");
        }
        else if (_currentSnoopText is not null && _currentReportPath is not null)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_currentReportPath)!);
                File.WriteAllText(_currentReportPath, _currentSnoopText);
                DiagStatus.Text = "Decode saved.";
                AppLog.Info($"Decode re-saved to {_currentReportPath}");
            }
            catch (Exception ex)
            {
                DiagStatus.Text = $"Save failed: {ex.Message}";
                AppLog.Error("Failed to re-save btsnoop decode", ex);
            }
        }
    }

    private void BtnImportSnoop_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import Android Bluetooth HCI snoop capture",
            Filter = "HCI captures (*.log;*.cfa;*.cfalog;*.btsnoop)|*.log;*.cfa;*.cfalog;*.btsnoop|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
        };
        if (dialog.ShowDialog(this) != true)
            return;

        var file = dialog.FileName;
        DiagStatus.Text = string.Empty;
        GattReportText.Text = "Decoding btsnoop capture...";
        AppLog.Info($"Importing btsnoop capture: {file}");

        try
        {
            var packets = new HciSnoopReader().Read(file);
            var text = BuildSnoopReport(file, packets);
            var reportPath = Path.Combine(
                DiagnosticReportStore.DefaultDirectory,
                $"btsnoop-decode-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.txt");

            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
            File.WriteAllText(reportPath, text);

            _currentReport = null;
            _currentSnoopText = text;
            _currentReportPath = reportPath;
            ReportPathText.Text = reportPath;
            BtnSaveReport.IsEnabled = true;
            GattReportText.Text = text;
            DiagStatus.Text =
                $"Decoded {packets.Count} ATT packets and saved to {reportPath}. These bytes are UNVERIFIED until a human confirms them against the real SP621E.";
            AppLog.Info($"btsnoop decode complete: {packets.Count} ATT packets -> {reportPath}");
        }
        catch (Exception ex)
        {
            GattReportText.Text = $"Import failed: {ex.Message}";
            DiagStatus.Text = $"Import failed: {ex.Message}";
            AppLog.Error("btsnoop import failed", ex);
        }
    }

    private static string BuildSnoopReport(string sourcePath, IReadOnlyList<AttPacket> packets)
    {
        var sb = new StringBuilder();
        sb.AppendLine("SP621E btsnoop decode (HciSnoopReader / HCI UART datalink 1002)");
        sb.AppendLine($"Source: {sourcePath}");
        sb.AppendLine($"ATT packets decoded: {packets.Count}");
        sb.AppendLine("------------------------------------------------------------------");
        foreach (var packet in packets)
            sb.AppendLine(packet.ToString());
        sb.AppendLine("------------------------------------------------------------------");
        sb.AppendLine("STATUS: UNVERIFIED evidence. Bytes require human confirmation against the");
        sb.AppendLine("real SP621E before any wire format is implemented (HARDWARE_PROTOCOL.md).");
        return sb.ToString();
    }

    // ------------------------------------------------------------------
    // Connection tab
    // ------------------------------------------------------------------

    private void OnConnDeviceDiscovered(object? sender, BleDeviceDiscoveredEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            var existing = _connDevices.FirstOrDefault(d => d.AddressValue == e.Device.AddressValue);
            if (existing is null)
                _connDevices.Add(e.Device);
            else
                _connDevices[_connDevices.IndexOf(existing)] = e.Device;
            ConnScanStatus.Text = $"Scanning... devices found: {_connDevices.Count}";
        });
    }

    private void OnConnScanFailed(object? sender, string message)
    {
        Dispatcher.BeginInvoke(() =>
        {
            ConnStatusNote.Text = message;
            AppLog.Error(message);
        });
    }

    private void BtnConnScan_Click(object sender, RoutedEventArgs e)
    {
        _connDevices.Clear();
        _connSelected = null;
        BtnConnect.IsEnabled = false;
        ConnStatusNote.Text = string.Empty;
        AppLog.Info("Starting BLE advertisement scan (connection tab).");

        try
        {
            _connScanner.Start();
            BtnConnScan.IsEnabled = false;
            BtnConnStopScan.IsEnabled = true;
        }
        catch (Exception ex)
        {
            ConnStatusNote.Text = $"Scan could not start: {ex.Message}";
            AppLog.Error("BLE scan failed to start (connection tab)", ex);
        }
    }

    private void BtnConnStopScan_Click(object sender, RoutedEventArgs e)
    {
        _connScanner.Stop();
        BtnConnScan.IsEnabled = true;
        BtnConnStopScan.IsEnabled = false;
        ConnScanStatus.Text = $"Scan stopped. Devices found: {_connDevices.Count}";
        AppLog.Info("BLE advertisement scan stopped (connection tab).");
    }

    private void ConnDeviceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _connSelected = ConnDeviceList.SelectedItem as BleDeviceInfo;
        BtnConnect.IsEnabled = _connSelected is not null && _driver?.IsConnected != true;
    }

    private async void BtnConnect_Click(object sender, RoutedEventArgs e)
    {
        var device = _connSelected;
        if (device is null)
            return;

        BtnConnect.IsEnabled = false;
        ConnStatusNote.Text = $"Connecting to {device.Name} [{device.Address}]...";
        AppLog.Info($"Connecting to {device.Name} [{device.Address}].");

        try
        {
            var oldDriver = _driver;
            _driver = null;
            if (oldDriver is not null)
                await oldDriver.DisconnectAsync();

            var transport = new WinrtBleTransport(device.AddressValue);
            var driver = new SP621EDriver(_engine.PixelCount, transport, null);
            _transport = transport;
            _driver = driver;

            await driver.ConnectAsync();

            ConnectionStatusText.Text = $"Connection: Connected ({device.Name})";
            ConnStatusNote.Text = "Connected. ";
            if (driver.Capabilities == ControllerCapability.None)
            {
                ConnStatusNote.Text +=
                    "No wire format is configured yet, so frames are NOT sent until a BanlanX HCI capture is imported (see HARDWARE_PROTOCOL.md §5).";
                AppLog.Warn("SP621E connected, but frame streaming is disabled (no wire format evidence yet).");
            }
            else
            {
                AppLog.Info("SP621E connected; controller is ready to receive frames.");
            }

            _frameSendFailuresLogged = 0;
            BtnDisconnect.IsEnabled = true;
            SaveSettings();
        }
        catch (Exception ex)
        {
            ConnectionStatusText.Text = "Connection: Failed";
            ConnStatusNote.Text = $"Connection failed: {ex.Message}";
            AppLog.Error("BLE connect failed", ex);
        }
        finally
        {
            BtnConnect.IsEnabled = _connSelected is not null;
        }
    }

    private async void BtnDisconnect_Click(object sender, RoutedEventArgs e)
    {
        BtnDisconnect.IsEnabled = false;
        try
        {
            var driver = _driver;
            _driver = null;
            if (driver is not null)
                await driver.DisconnectAsync();
            var transport = _transport;
            if (transport is not null)
            {
                _transport = null;
                await transport.DisposeAsync();
            }
            ConnectionStatusText.Text = "Connection: Disconnected";
            ConnStatusNote.Text = "Disconnected.";
            AppLog.Info("Disconnected from SP621E.");
        }
        catch (Exception ex)
        {
            ConnStatusNote.Text = $"Disconnect failed: {ex.Message}";
            AppLog.Error("BLE disconnect failed", ex);
        }
        finally
        {
            BtnConnect.IsEnabled = _connSelected is not null;
        }
    }

    // ------------------------------------------------------------------
    // Audio Source tab
    // ------------------------------------------------------------------

    private void BtnRefreshAudio_Click(object sender, RoutedEventArgs e)
    {
        RefreshAudioDevices();
    }

    private void AudioSourceMode_Checked(object sender, RoutedEventArgs e)
    {
        if (RadioSourceInput is null || RadioSourceLoopback is null || AudioStatusText is null)
            return;
        RefreshAudioDevices();
    }

    private void RefreshAudioDevices()
    {
        try
        {
            var isInput = RadioSourceInput.IsChecked == true;
            var status = isInput ? "Input endpoints" : "Output endpoints";
            _audioDevices = isInput ? AudioDeviceEnumerator.GetCaptureDevices()
                                    : AudioDeviceEnumerator.GetRenderDevices();
            AudioDeviceCombo.ItemsSource = _audioDevices;
            AudioStatusText.Text = $"{status}: {_audioDevices.Count}";

            if (_audioDevices.Count == 0)
            {
                AudioStatusText.Text += " (none found; is the input device enabled in Windows Sound settings?)";
            }
            else if (!string.IsNullOrEmpty(_settings.LastAudioDeviceId))
            {
                var match = _audioDevices.FirstOrDefault(d => d.Id == _settings.LastAudioDeviceId);
                if (match is not null)
                    AudioDeviceCombo.SelectedItem = match;
            }
            AppLog.Info($"Audio device refresh ({status}): {_audioDevices.Count} endpoints.");
        }
        catch (Exception ex)
        {
            AudioStatusText.Text = $"Could not enumerate audio devices: {ex.Message}";
            AppLog.Error("Audio device enumeration failed", ex);
        }
    }

    private void AudioDeviceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        BtnStartAudio.IsEnabled = AudioDeviceCombo.SelectedItem is AudioDeviceInfo && _audioSource is null;
    }

    private void BtnStartAudio_Click(object sender, RoutedEventArgs e)
    {
        var device = AudioDeviceCombo.SelectedItem as AudioDeviceInfo;
        if (device is null)
        {
            AudioStatusText.Text = "Select an output device first.";
            return;
        }

        try
        {
            StopAudioSource();
            var isInput = RadioSourceInput.IsChecked == true;
            IAudioSource source = isInput
                ? new WasapiInputAudioSource(device.Id, device.FriendlyName)
                : new WasapiLoopbackAudioSource(device.Id, device.FriendlyName);
            _audioSource = source;
            _analyzer = new AudioAnalyzer();
            _beatDetector = new BeatDetector();
            source.Failed += OnAudioFailed;
            source.SamplesAvailable += OnSamplesAvailable;
            source.Start();

            _settings.LastAudioDeviceId = device.Id;
            _settings.IsInputAudioSource = isInput;
            SaveSettings();

            var kind = isInput ? "input device" : "output (loopback)";
            AudioStatusText.Text =
                $"Listening to: {device.FriendlyName}  ({kind}, {source.SampleRate} Hz, {source.ChannelCount} ch)";
            BtnStartAudio.IsEnabled = false;
            BtnStopAudio.IsEnabled = true;
            AppLog.Info($"Audio capture started on {device.FriendlyName} ({kind}).");
        }
        catch (Exception ex)
        {
            StopAudioSource();
            AudioStatusText.Text = $"Could not start capture: {ex.Message}";
            AppLog.Error("Audio capture failed to start", ex);
        }
    }

    private void BtnStopAudio_Click(object sender, RoutedEventArgs e)
    {
        StopAudioSource();
        StopAudioButtons();
        AudioStatusText.Text = "Capture stopped.";
        AppLog.Info("Audio capture stopped.");
    }

    private void OnSamplesAvailable(object? sender, AudioSamplesAvailableEventArgs e)
    {
        var analyzer = _analyzer;
        var beatDetector = _beatDetector;
        if (analyzer is null || beatDetector is null)
            return;

        var data = analyzer.Analyze(e.Samples, e.SampleRate, e.ChannelCount, e.CapturedAt);
        beatDetector.Update(data);
        _latestAudio = data;

        var meter = data;
        Dispatcher.BeginInvoke(() =>
        {
            RmsMeter.Value = Math.Clamp(meter.Rms, 0.0, RmsMeter.Maximum);
            RmsText.Text = $"level {meter.Rms:F3}";
            BpmText.Text = meter.EstimatedBpm > 0 ? $"{meter.EstimatedBpm:F0} BPM" : "—";
            BeatStatusText.Text = meter.IsBeat ? "● Beat" : "○";
            BeatStatusText.Foreground = meter.IsBeat ? Brushes.Green : Brushes.Gray;
        });
    }

    private void OnAudioFailed(object? sender, AudioFailureEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            AudioStatusText.Text = e.Message;
            AppLog.Error(e.Message, e.Inner);
            StopAudioSource();
            StopAudioButtons();
        });
    }

    private void StopAudioSource()
    {
        var source = _audioSource;
        _audioSource = null;
        _latestAudio = null;
        if (source is not null)
        {
            try
            {
                source.SamplesAvailable -= OnSamplesAvailable;
                source.Failed -= OnAudioFailed;
            }
            catch
            {
            }
            try { source.Stop(); } catch { }
            (source as IDisposable)?.Dispose();
        }
    }

    private void StopAudioButtons()
    {
        BtnStartAudio.IsEnabled = AudioDeviceCombo.SelectedItem is AudioDeviceInfo;
        BtnStopAudio.IsEnabled = false;
        RmsMeter.Value = 0;
        BpmText.Text = "—";
        BeatStatusText.Text = "○";
        BeatStatusText.Foreground = Brushes.Gray;
    }

    // ------------------------------------------------------------------
    // Effects tab
    // ------------------------------------------------------------------

    private void EffectCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _engine.Registry.ActiveEffect = EffectCombo.SelectedItem as IEffect;
        EffectDescriptionText.Text = _engine.Registry.ActiveEffect?.Description ?? "";
        if (_ready)
            SaveSettings();
    }

    private void IntensitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready)
            return;
        _engine.Intensity = IntensitySlider.Value;
        SaveSettings();
    }

    private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready)
            return;
        _engine.Speed = SpeedSlider.Value;
        SaveSettings();
    }

    private void BrightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready)
            return;
        _engine.GlobalBrightness = BrightnessSlider.Value;
        UpdateSliderValueLabels();
        SaveSettings();
    }

    private void InitializePreview(int pixelCount)
    {
        _previewBrushes.Clear();
        for (var i = 0; i < pixelCount; i++)
            _previewBrushes.Add(new SolidColorBrush(Colors.Black));
        LedPreview.ItemsSource = _previewBrushes;
    }

    private void BtnStartRender_Click(object sender, RoutedEventArgs e)
    {
        if (ScreenEnableCheck.IsChecked == true)
            ScreenEnableCheck.IsChecked = false; // screen sync and audio effects are mutually exclusive
        _clock.Restart();
        _renderTimer.IsEnabled = true;
        _renderAllDark = false; // force status refresh on first tick
        BtnStartRender.IsEnabled = false;
        BtnStopRender.IsEnabled = true;
        PipelineStatusText.Text = "Pipeline: Running";
        AppLog.Info("Effects render loop started.");
    }

    private void BtnStopRender_Click(object sender, RoutedEventArgs e)
    {
        StopEffectsRender();
    }

    private void StopEffectsRender()
    {
        _renderTimer.IsEnabled = false;
        BtnStartRender.IsEnabled = true;
        BtnStopRender.IsEnabled = false;
        foreach (var brush in _previewBrushes)
            brush.Color = Colors.Black;
        _renderAllDark = true;
        PipelineStatusText.Text = "Pipeline: Stopped";
        RenderStatusText.Text = "Start the audio capture, then start the preview.";
        AppLog.Info("Effects render loop stopped.");
    }

    private void RenderTimer_Tick(object? sender, EventArgs e)
    {
        var audio = _latestAudio ?? new AudioData(DateTimeOffset.UtcNow);
        var frame = _engine.Render(audio, _clock.Elapsed * _engine.Speed);
        UpdatePreview(frame);
        SendFrame(frame);
        UpdateRenderStatus(frame);
    }

    private void UpdateRenderStatus(RgbFrame frame)
    {
        var allDark = true;
        foreach (var p in frame.Pixels)
        {
            if (p.R != 0 || p.G != 0 || p.B != 0)
            {
                allDark = false;
                break;
            }
        }

        if (allDark == _renderAllDark)
            return;
        _renderAllDark = allDark;

        if (allDark)
        {
            RenderStatusText.Text = _engine.GlobalBrightness <= 0.001
                ? "All LEDs dark: Brightness is at 0% — drag the Brightness slider up."
                : "All LEDs dark: no audio energy yet. Play music, or use Screen Sync instead.";
        }
        else
        {
            RenderStatusText.Text = $"Previewing '{ActiveEffectName}' at ~30 fps.";
        }
    }

    private void UpdatePreview(RgbFrame frame)
    {
        var count = Math.Min(frame.Pixels.Count, _previewBrushes.Count);
        for (var i = 0; i < count; i++)
        {
            var c = frame.Pixels[i];
            _previewBrushes[i].Color = Color.FromRgb(c.R, c.G, c.B);
        }
    }

    private void SendFrame(RgbFrame frame)
    {
        var driver = _driver;
        if (driver is null || !driver.IsConnected || !driver.Capabilities.HasFlag(ControllerCapability.FrameStreaming))
            return;

        _ = driver.SendFrameAsync(frame).ContinueWith(t =>
        {
            if (t.Exception is null)
                return;
            var ex = t.Exception.GetBaseException();
            Dispatcher.BeginInvoke(() =>
            {
                if (_frameSendFailuresLogged < 3)
                {
                    _frameSendFailuresLogged++;
                    AppLog.Error($"Frame send failed: {ex.Message}");
                }
            });
        }, CancellationToken.None);
    }

    // ------------------------------------------------------------------
    // Screen Sync tab
    // ------------------------------------------------------------------

    private sealed record ScreenModeOption(ScreenSyncMode Mode, string DisplayName);

    private ScreenSyncMode CurrentScreenMode() =>
        (ScreenModeCombo.SelectedItem as ScreenModeOption)?.Mode ?? ScreenSyncMode.Average;

    private void ScreenEnableCheck_Checked(object? sender, RoutedEventArgs e) => StartScreenSync();

    private void ScreenEnableCheck_Unchecked(object? sender, RoutedEventArgs e) => StopScreenSync();

    private void ScreenModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready)
            return;
        _screenPipeline.Reset();
        SaveSettings();
    }

    private void ScreenSmoothSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready)
            return;
        UpdateSliderValueLabels();
        SaveSettings();
    }

    private void ScreenBrightSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready)
            return;
        UpdateSliderValueLabels();
        SaveSettings();
    }

    private void InitializeScreenPalette(int swatches)
    {
        _screenPaletteBrushes.Clear();
        for (var i = 0; i < swatches; i++)
            _screenPaletteBrushes.Add(new SolidColorBrush(Colors.Black));
    }

    private void StartScreenSync()
    {
        if (!_ready)
            return;
        if (_renderTimer.IsEnabled)
            StopEffectsRender();

        _screenSampler ??= new ScreenSampler();
        _screenCaptureInFlight = false;
        _screenLatest = null;
        _screenCaptureErrorLogged = false;
        _screenPipeline.Reset();
        _screenTimer.IsEnabled = true;
        PipelineStatusText.Text = "Pipeline: Screen sync";
        RenderStatusText.Text = "Preview is driven by Screen Sync (Effects preview paused).";
        ScreenStatusText.Text = "Sampling display...";
        AppLog.Info("Screen color sync started.");
    }

    private void StopScreenSync()
    {
        if (!_ready)
            return;
        _screenTimer.IsEnabled = false;
        _screenPaletteBrushes.ToList().ForEach(b => b.Color = Colors.Black);
        _previewBrushes.ToList().ForEach(b => b.Color = Colors.Black);
        ScreenAvgText.Text = string.Empty;
        PipelineStatusText.Text = "Pipeline: Stopped";
        ScreenStatusText.Text = "Screen sync is off.";
        AppLog.Info("Screen color sync stopped.");
    }

    private void ScreenTimer_Tick(object? sender, EventArgs e)
    {
        var sampler = _screenSampler;
        if (sampler is null)
            return;

        if (!_screenCaptureInFlight)
        {
            _screenCaptureInFlight = true;
            _ = Task.Run(() => sampler.Capture()).ContinueWith(t =>
            {
                _screenCaptureInFlight = false;
                if (t.Result is not null)
                    _screenLatest = t.Result;
            }, CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
        }

        var snap = _screenLatest;
        if (snap is null)
        {
            if (sampler.LastError is not null)
            {
                if (!_screenCaptureErrorLogged)
                {
                    _screenCaptureErrorLogged = true;
                    AppLog.Error($"Screen capture unavailable: {sampler.LastError}");
                }
                ScreenStatusText.Text = $"Screen capture unavailable: {sampler.LastError}";
            }
            return;
        }
        _screenCaptureErrorLogged = false;

        var frame = _screenPipeline.Step(snap, CurrentScreenMode(), ScreenSmoothSlider.Value, ScreenBrightSlider.Value);
        UpdatePreview(frame);
        SendFrame(frame);
        UpdateScreenPalette(snap);

        if (ScreenBrightSlider.Value <= 0.001)
        {
            ScreenStatusText.Text = "LEDs dark: Screen brightness is at 0% — drag the Screen brightness slider up.";
        }
        else if (IsFrameDark(frame))
        {
            ScreenStatusText.Text =
                $"Sampled frame is dark (mostly black screen on {snap.Width}×{snap.Height} cells). LEDs stay dim until bright content appears.";
        }
        else
        {
            ScreenStatusText.Text = $"Sampling {snap.Width}×{snap.Height} cells · ~20 fps · avg {snap.Average}";
        }
    }

    private static bool IsFrameDark(RgbFrame frame)
    {
        foreach (var p in frame.Pixels)
        {
            if (p.R != 0 || p.G != 0 || p.B != 0)
                return false;
        }
        return true;
    }

    private void UpdateScreenPalette(ScreenSnapshot snap)
    {
        const int swatches = 8;
        var w = snap.Width;
        for (var i = 0; i < _screenPaletteBrushes.Count && i < swatches; i++)
        {
            var col0 = (long)i * w / swatches;
            var col1 = (long)(i + 1) * w / swatches;
            if (col1 <= col0)
                col1 = col0 + 1;

            long r = 0, g = 0, b = 0;
            var count = 0;
            for (var row = 0; row < snap.Height; row++)
            {
                for (var col = (int)col0; col < col1; col++)
                {
                    var c = snap.Pixels[row * w + col];
                    r += c.R;
                    g += c.G;
                    b += c.B;
                    count++;
                }
            }
            _screenPaletteBrushes[i].Color = count == 0
                ? Color.FromRgb(snap.Average.R, snap.Average.G, snap.Average.B)
                : Color.FromRgb((byte)(r / count), (byte)(g / count), (byte)(b / count));
        }
        ScreenAvgText.Text = $"Whole-screen average: {snap.Average}";
    }
}