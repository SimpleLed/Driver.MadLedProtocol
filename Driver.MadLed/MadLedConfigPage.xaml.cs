using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using SimpleLed;

namespace Driver.MadLed
{
    /// <summary>
    /// Interaction logic for MadLedConfigPage.xaml
    /// </summary>
    public partial class MadLedConfigPage : UserControl
    {
        private MadLed MadLed;
        public MadLedConfigPage(MadLed madled)
        {
            MadLed = madled;
            InitializeComponent();
            DevicesDropDown.Items.Clear();

            foreach (string connectedMadLedUnit in MadLed.ConnectedMadLedUnits)
            {
                DevicesDropDown.Items.Add(connectedMadLedUnit);
            }
        }

        private MadLed.MadLedDevice madLedDevice = null;
        private void DevicesDropDown_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string device = DevicesDropDown.SelectedItem as string;
            madLedDevice = MadLed.MadLedDevices.First(x => x.Serial == device);

            PinsView.Items.Clear();

            for (int i = 0; i < 8; i++)
            {
                MadLed.MadLedDevice.PinConfig pc = madLedDevice.GetConfigFromPin(i + 1, madLedDevice.stream);


                PinViewModel mdl = null;
                if (pc != null && pc.LedCount > 0)
                {
                    mdl = new PinViewModel
                    {
                        Pin = i + 1,
                        DeviceClass = pc.DeviceClass,
                        LedCount = pc.LedCount,
                        Name = pc.Name
                    };
                }
                else
                {
                    mdl = new PinViewModel
                    {
                        Pin = i + 1,
                        DeviceClass = -1,
                        LedCount = 0,
                        Name = ""
                    };
                }

                PinsView.Items.Add(mdl);
            }

        }

        public class PinViewModel
        {
            public int Pin { get; set; }
            public string Name { get; set; }
            public int DeviceClass { get; set; }
            public int LedCount { get; set; }

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;


            PinViewModel mdl = button.DataContext as PinViewModel;
            SetUp(mdl, false);
        }

        private void SetUp(PinViewModel mdl, bool isPermo)
        {

            MadLed.MadLedDevice.PinConfig pc = new MadLed.MadLedDevice.PinConfig
            {
                Name = mdl.Name,
                DeviceClass = mdl.DeviceClass,
                Pin = mdl.Pin,
                LedCount = mdl.LedCount
            };

            var pcfg = madLedDevice.SetConfigCmd(mdl.Pin, pc);
            if (isPermo)
            {
                pcfg[1] = 10;
            }

            //var pc = config.PinConfigs[i];
            madLedDevice.SendPacket(madLedDevice.stream, pcfg);
            madLedDevice.ReadReturnReport(madLedDevice.stream);

            MadLed.MadLedControlDevice mlcd = new MadLed.MadLedControlDevice
            {
                ConnectedTo = "Channel " + (mdl.Pin),
                DeviceType = MadLed.deviceTypes[pc.DeviceClass],
                Name = pc.Name,
                MadLedDevice = madLedDevice,
                LEDs = new ControlDevice.LedUnit[pc.LedCount],
                Driver = MadLed,
                Pin = mdl.Pin
            };

            for (int p = 0; p < pc.LedCount; p++)
            {
                mlcd.LEDs[p] = new ControlDevice.LedUnit
                {
                    Data = new ControlDevice.LEDData
                    {
                        LEDNumber = p
                    },
                    LEDName = "LED " + (p + 1),
                    Color = new LEDColor(0, 0, 0)
                };
            }

            MadLed.InvokeAdded(mlcd);
            //DeviceAdded?.Invoke(this, new Events.DeviceChangeEventArgs(mlcd));
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;


            PinViewModel mdl = button.DataContext as PinViewModel;
            SetUp(mdl, true);
        }
    }
}
