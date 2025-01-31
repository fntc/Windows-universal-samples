//*********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************

using System;
using System.Diagnostics;
using System.Threading;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.Devices.PointOfService;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;


namespace SDKTemplate
{
    public sealed partial class Scenario1_FindClaimEnable : Page
    {
        private MainPage rootPage = MainPage.Current;
        bool isBusy = false;
        private Timer _timer;

        public Scenario1_FindClaimEnable()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            RetainDeviceCheckBox.IsChecked = rootPage.IsAnImportantTransaction;
            rootPage.StateChanged += UpdateButtons;
            UpdateButtons();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            rootPage.StateChanged -= UpdateButtons;
        }

        void UpdateButtons()
        {
            PrinterNameRun.Text = (rootPage.Printer == null) ? "None" : rootPage.deviceInfo.Name + " (" + rootPage.Printer.DeviceId + ")";
            if (isBusy)
            {
                FindButton.IsEnabled = false;
                ClaimAndEnableButton.IsEnabled = false;
                ReleaseClaimedPrinterButton.IsEnabled = false;
                ReleaseAllPrintersButton.IsEnabled = false;
            }
            else if (rootPage.Printer == null)
            {
                FindButton.IsEnabled = true;
                ClaimAndEnableButton.IsEnabled = false;
                ReleaseClaimedPrinterButton.IsEnabled = false;
                ReleaseAllPrintersButton.IsEnabled = false;
            }
            else
            {
                FindButton.IsEnabled = false;
                ReleaseAllPrintersButton.IsEnabled = true;
                if (rootPage.ClaimedPrinter == null)
                {
                    ClaimAndEnableButton.IsEnabled = true;
                    ReleaseClaimedPrinterButton.IsEnabled = false;
                }
                else
                {
                    ClaimAndEnableButton.IsEnabled = false;
                    ReleaseClaimedPrinterButton.IsEnabled = true;
                }
            }
        }

        async void FindPrinter_Click()
        {
            isBusy = true;
            UpdateButtons();
            rootPage.NotifyUser("", NotifyType.StatusMessage);

            rootPage.ReleaseAllPrinters();

            // Select a PosPrinter device using the Device Picker.
            DevicePicker devicePicker = new DevicePicker();
            devicePicker.Filter.SupportedDeviceSelectors.Add(PosPrinter.GetDeviceSelector());

            // Anchor the picker on the Find button.
            GeneralTransform ge = FindButton.TransformToVisual(Window.Current.Content as UIElement);
            Rect rect = ge.TransformBounds(new Rect(0, 0, FindButton.ActualWidth, FindButton.ActualHeight));

            DeviceInformation deviceInfo = await devicePicker.PickSingleDeviceAsync(rect);
            rootPage.deviceInfo = deviceInfo;
            PosPrinter printer = null;
            if (deviceInfo != null)
            {
                printer = await PosPrinter.FromIdAsync(deviceInfo.Id);
            }
            if (printer != null && printer.Capabilities.Receipt.IsPrinterPresent)
            {
                rootPage.Printer = printer;
                rootPage.NotifyUser("Found receipt printer.", NotifyType.StatusMessage);
            }
            else
            {
                // Get rid of the printer we can't use.
                printer?.Dispose();
                rootPage.NotifyUser("Please select a device whose printer is present.", NotifyType.ErrorMessage);
            }

            isBusy = false;
            UpdateButtons();
        }

        async void ClaimAndEnable_Click()
        {
            isBusy = true;
            UpdateButtons();
            rootPage.ClaimedPrinter = await rootPage.Printer.ClaimPrinterAsync();
            if (rootPage.ClaimedPrinter == null)
            {
                rootPage.NotifyUser("Unable to claim printer", NotifyType.ErrorMessage);
            }
            else
            {
                rootPage.NotifyUser("Claimed printer", NotifyType.StatusMessage);

                // Register for the ReleaseDeviceRequested event so we know when somebody
                // wants to claim the printer away from us.
                rootPage.SubscribeToReleaseDeviceRequested();

                rootPage.ClaimedPrinter.Closed += (sender, args) => { _timer?.Dispose(); };

                if (await rootPage.ClaimedPrinter.EnableAsync())
                {
                    rootPage.NotifyUser("Enabled printer.", NotifyType.StatusMessage);
                    

                    _timer = new Timer(UpdateStatus, rootPage.ClaimedPrinter, TimeSpan.Zero, TimeSpan.FromMilliseconds(500));
                    rootPage.Printer.StatusUpdated += async (sender, args) =>
                    {
                        //BUG: StatusUpdated is never fired!
                        await MainPage.Current.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                        {
                            rootPage.NotifyUser("Status updated event: " + args.Status.StatusKind.ToString(), NotifyType.StatusMessage);
                            UpdateStatus(rootPage.ClaimedPrinter.Receipt);
                        });
                    };

                }
                else
                {
                    rootPage.NotifyUser("Could not enable printer", NotifyType.ErrorMessage);
                    rootPage.ReleaseClaimedPrinter();
                }
            }

            isBusy = false;
            UpdateButtons();
        }

        private void UpdateStatus(object state)
        {
            var sw = Stopwatch.StartNew();
            //var health = rootPage.Printer.CheckHealthAsync(UnifiedPosHealthCheckLevel.POSInternal).GetAwaiter().GetResult();
            var claimed = state as ClaimedPosPrinter;
            //var enabled = claimed.IsEnabled;
            var printer = claimed.Receipt;
            
            var coverOpen = printer.IsCoverOpen;
            var empty = printer.IsCartridgeEmpty;
            var removed = printer.IsCartridgeRemoved;
            var cleaning = printer.IsHeadCleaning;
            var paperempty = printer.IsPaperEmpty;
            var papernearend = printer.IsPaperNearEnd;
            var ready = printer.IsReadyToPrint;
            sw.Stop();
            Debug.WriteLine($"{sw.ElapsedMilliseconds}ms to query {coverOpen},{empty},{removed},{cleaning},{paperempty},{papernearend} - {ready}");
        }

        void IsImportantTransaction_Click()
        {
            rootPage.IsAnImportantTransaction = RetainDeviceCheckBox.IsChecked.Value;
        }

        void ReleaseClaim_Click()
        {
            rootPage.ReleaseClaimedPrinter();
        }

        void ReleaseAll_Click()
        {
            rootPage.ReleaseAllPrinters();
        }
    }
}
