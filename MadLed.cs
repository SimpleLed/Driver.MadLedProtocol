using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Controls;
using HidSharp;
using MarkdownUI.WPF;
using SimpleLed;
using DeviceTypes = SimpleLed.DeviceTypes;
using Timer = System.Timers.Timer;

namespace Driver.MadLed
{
    public class MadLed : ISimpleLedWithConfig
    {

        public List<CustomDeviceSpecification> GetCustomDeviceSpecifications()
        {
            return new List<CustomDeviceSpecification>
            {

            };
        }

        public static int PacketSize = 64;
        public void Dispose()
        {

        }

        private MadLedDevice.MadLedConfig config;

        public event Events.DeviceChangeEventHandler DeviceAdded;
        public event Events.DeviceChangeEventHandler DeviceRemoved;

        private List<USBDevice> SupportedDevices = new List<USBDevice>
        {
            new USBDevice{VID = 0x1b4f, HID = 0x9207},
            new USBDevice{VID = 0x1b4f, HID = 0x9204},
            new USBDevice{VID = 0x2341, HID = 0x8036},
            new USBDevice{VID = 0x1B1C, HID = 0x0C0B},
            new USBDevice{VID = 0x1B4F, HID = 0x9206}
        };
        public void Configure(DriverDetails driverDetails)
        {
            config = new MadLedDevice.MadLedConfig();
            config.PinConfigs = new MadLedDevice.PinConfig[8];
            var connected = SimpleLed.SLSManager.GetSupportedDevices(SupportedDevices);
            foreach (USBDevice supportedDevice in connected)
            {
                InterestedUSBChange(supportedDevice.VID, supportedDevice.HID.Value, true);
            }
        }

        private float globalBrightness = 0.5f;
        public void Push(ControlDevice controlDevice)
        {
            MadLedControlDevice mlcd = (MadLedControlDevice)controlDevice;

            byte[] leds = new byte[mlcd.LEDs.Length * 3];

            for (int i = 0; i < mlcd.LEDs.Length; i++)
            {

                leds[(i * 3) + 0] = (byte)(mlcd.LEDs[i].Color.Blue * globalBrightness);
                leds[(i * 3) + 1] = (byte)(mlcd.LEDs[i].Color.Green * globalBrightness);
                leds[(i * 3) + 2] = (byte)(mlcd.LEDs[i].Color.Red * globalBrightness);
            }

            mlcd.MadLedDevice.SendLeds(mlcd.MadLedDevice.stream, leds, (byte)mlcd.Pin);
        }

        public void Pull(ControlDevice controlDevice)
        {

        }

        public DriverProperties GetProperties()
        {
            return new DriverProperties
            {
                SupportsPull = false,
                SupportsPush = true,
                IsSource = false,
                SupportsCustomConfig = true,
                ProductId = Guid.Parse("5dcf474c-4bd0-4516-b871-f98ca885dbb0"),
                Author = "MadNinja",
                Blurb = "Driver for controlling MadLed controllers.",
                CurrentVersion = new ReleaseNumber(1, 0, 1, AutoRevision.BuildRev.BuildRevision),
                GitHubLink = "https://github.com/SimpleLed/Driver.MadLedProtocol",
                IsPublicRelease = false,
                SupportedDevices = this.SupportedDevices,
                SetDeviceOverride = SetDeviceOverride,
                DeviceSpecifications = GetCustomDeviceSpecifications(),
                GetMappers = GetMappers
            };
        }

        private List<Type> GetMappers()
        {
            return new List<Type>();
        }



        public T GetConfig<T>() where T : SLSConfigData
        {
            MadLedDevice.MadLedConfig data = this.config;
            SLSConfigData proxy = data;
            return (T)proxy;
        }

        public void SetColorProfile(ColorProfile value)
        {

        }


        public void PutConfig<T>(T config) where T : SLSConfigData
        {
            this.config = config as MadLedDevice.MadLedConfig;
        }

        public string Name()
        {
            return "MadLed";
        }

        public static List<string> ConnectedMadLedUnits = new List<string>();

        public static List<MadLedDevice> MadLedDevices = new List<MadLedDevice>();
        public void InterestedUSBChange(int VID, int PID, bool connected)
        {
            if (connected)
            {
                var madLedDevices = MadLedDevice.GetMadLedDevices(VID, PID, this);

                foreach (var m in madLedDevices)
                {
                    var remove = MadLedDevices.Where(x => x.Serial == m.Serial);
                    foreach (MadLedDevice madLedDevice in remove.ToList())
                    {
                        MadLedDevices.Remove(madLedDevice);
                    }

                    ConnectedMadLedUnits.Add(m.Serial);
                    MadLedDevices.Add(m);
                }
            }
            else
            {

            }
        }

        public void SetDeviceOverride(ControlDevice controlDevice, CustomDeviceSpecification deviceSpec)
        {
            controlDevice.LEDs = new ControlDevice.LedUnit[deviceSpec.LedCount];

            for (int p = 0; p < deviceSpec.LedCount; p++)
            {
                controlDevice.LEDs[p] = new ControlDevice.LedUnit
                {
                    Data = new ControlDevice.LEDData
                    {
                        LEDNumber = p
                    },
                    LEDName = "LED " + (p + 1),
                    Color = new LEDColor(0, 0, 0)
                };
            }

            controlDevice.CustomDeviceSpecification = deviceSpec;
        }

        public static string[] deviceTypes = new[]
        {
            "Fan",
            "LedStrip",
            "GPU",
            "MotherBoard",
            "Keyboard",
            "Keypad",
            "Mouse",
            "MousePad",
            "Headset",
            "HeadsetStand",
            "PSU",
            "Cooler",
            "Memory",
            "Speaker",
            "Bulb",
            "Other",
            "AIO",
            "WaterBlock"
        };


        public class MadLedControlDevice : ControlDevice
        {
            public MadLedDevice MadLedDevice { get; set; }
            public int Pin { get; set; }
        }

        public class MadLedDevice
        {
            public List<MadLedControlDevice> ControlDevices = new List<MadLedControlDevice>();
            public HidStream stream = null;
            public int VID;
            public int PID;
            public string Serial;
            public byte[] SupportedPins;
            public MadLed Driver { get; set; }

            private T HandleIncoming<T>(byte[] data)
            {
                switch (data[2])
                {
                    case (127):
                        byte pin = data[3];
                        break;
                    case (100):
                        SupportedPins = new byte[data.Length - 3];

                        int ii = 3;
                        List<byte> supTempt = new List<byte>();
                        while (ii < 64)
                        {
                            byte sp = data[ii];
                            if (sp == 0)
                            {
                                break;
                            }

                            supTempt.Add(sp);

                            ii++;
                        }

                        return (T)((object)supTempt.ToArray());

                    case (134):
                        var pc = new PinConfig();

                        pc.Pin = data[3];
                        pc.LedCount = data[4];
                        pc.DeviceClass = data[5];

                        for (int i = 6; i < 6 + 16; i++)
                        {
                            byte t = data[i];

                            if (t == 13 || t == 0)
                            {
                                break;
                            }
                            else
                            {
                                pc.Name = pc.Name + (char)t;
                            }


                        }

                        return (T)((object)pc);


                    case (2):

                        string r = "";

                        for (int i = 0; i < 16; i++)
                        {
                            byte t = data[i + 3];

                            if (t == 13 || t == 0)
                            {
                                break;
                            }
                            else
                            {
                                r = r + (char)t;
                            }
                        }

                        //Debug.WriteLine("----------------------------------------------");
                        //Debug.WriteLine(r);
                        //Debug.WriteLine("----------------------------------------------");

                        if (!r.StartsWith("MLG4"))
                        {
                            return default;
                        }

                        return (T)((object)r.Substring(4).Trim());


                    default:
                        Debug.WriteLine(data[2]);
                        break;
                }

                return default;
            }


            private MadLedControlDevice SetUpPin(PinConfig pc)
            {
                if (pc.LedCount > 0 && pc.Name != null)
                {
                    string dt = "";
                    if (pc.DeviceClass >= 0 && pc.DeviceClass < deviceTypes.Length)
                    {
                        dt = deviceTypes[pc.DeviceClass];


                        MadLedControlDevice mlcd = new MadLedControlDevice
                        {
                            ChannelUniqueId = "Pin " + pc.Pin,
                            DeviceType = dt,
                            Name = pc.Name.Trim(),
                            MadLedDevice = this,
                            LEDs = new ControlDevice.LedUnit[pc.LedCount],
                            Driver = this.Driver,
                            Pin = pc.Pin,
                            OverrideSupport = OverrideSupport.All,
                            CustomDeviceSpecification = new GenericCooler()
                        };

                        for (int p = 0; p < pc.LedCount; p++)
                        {
                            mlcd.LEDs[p] = new ControlDevice.LedUnit
                            {
                                Data = new ControlDevice.LEDData { LEDNumber = p },
                                LEDName = "LED " + (p + 1),
                                Color = new LEDColor(0, 0, 0)
                            };
                        }

                        return mlcd;
                    }
                }

                return null;
            }

            public MadLedDevice()
            {

            }

            public static List<MadLedDevice> GetMadLedDevices(int vid, int pid, MadLed madLed)
            {
                List<MadLedDevice> results = new List<MadLedDevice>();
                var terp = new OpenConfiguration();
                terp.SetOption(OpenOption.Transient, true);

                var loader = new HidDeviceLoader();
                HidDevice device = null;
                HidSharp.HidDevice[] devices = null;

                HidSharp.HidDevice[] tempdevices = loader.GetDevices(vid).ToArray();
                if (tempdevices.Length > 0)
                {
                    devices = tempdevices;
                }


                int attempts = 0;

                if (devices != null)
                {
                    int channelCount = 0;
                    foreach (HidDevice hidDevice in devices)
                    {

                        try
                        {

                            device = hidDevice;
                            byte[] t = device.GetRawReportDescriptor();
                            var dddd = (device.GetFriendlyName());
                            MadLedDevice nmld = new MadLedDevice();
                            nmld.Driver = madLed;
                            nmld.stream = device.Open(terp);

                            nmld.stream.ReadTimeout = 1000;
                            nmld.stream.WriteTimeout = 10000;

                            string serial = nmld.GetSerial(nmld.stream);
                            channelCount++;
                            ControlChannel controlChannel = new ControlChannel()
                            {
                                Name = "MadLed " + channelCount + " (" + dddd + ")",
                                Serial = serial
                            };

                            if (!string.IsNullOrWhiteSpace(serial))
                            {
                                nmld.Serial = serial;
                                nmld.DeviceName = dddd;
                                var pins = nmld.GetSupportedPins();
                                nmld.SupportedPins = pins;
                                foreach (byte pin in pins)
                                {
                                    var pc = nmld.GetConfigFromPin(pin, nmld.stream);
                                    //nmld.SendPacket(nmld.stream, nmld.SetConfigCmd(pin,pc));
                                    var mlcd = nmld.SetUpPin(pc);
                                    if (mlcd != null)
                                    {
                                        mlcd.ControlChannel = controlChannel;
                                        nmld.ControlDevices.Add(mlcd);
                                        nmld.Driver.InvokeAdded(mlcd);

                                    }
                                }


                                nmld.VID = vid;
                                nmld.PID = pid;

                                results.Add(nmld);
                            }


                        }
                        catch (Exception ep)
                        {
                            Console.WriteLine(ep.Message);
                            attempts++;
                            device = null;
                            Thread.Sleep(100);
                        }
                    }
                }

                return results;
            }

            public string DeviceName { get; set; }


            public T ReadUsb<T>()
            {
                isTryingToReadUSB = true;

                try
                {
                    if (stream != null)
                    {
                        if (stream.CanRead)
                        {
                            byte[] temp = new byte[64];
                            var t = stream.Read(temp);

                            if (t > 0)
                            {
                                int cmd = temp[2];
                                return (T)HandleIncoming<T>(temp);
                            }
                        }
                    }
                }
                catch
                {
                }


                isTryingToReadUSB = false;
                return default;

            }

            private bool isTryingToReadUSB = false;

            public T SendPacket<T>(HidStream stream, byte[] packet, bool expandTo64Bytes = true)
            {
                SendPacket(stream, packet, expandTo64Bytes);
                return ReadUsb<T>();
            }
            public void SendPacket(HidStream stream, byte[] packet, bool expandTo64Bytes = true)
            {
                try
                {
                    while (isSending)
                    {
                        Thread.Sleep(0);
                    }

                    if (packet.Length < 64)
                    {
                        byte[] tmp = new byte[64];
                        for (int i = 0; i < packet.Length; i++)
                        {
                            tmp[i] = packet[i];
                        }

                        packet = tmp;
                    }

                    isSending = true;
                    stream.Write(packet);
                    stream.Flush();
                }
                catch { }

                isSending = false;
            }

            public bool isSending = false;

            short[] Get16BitColorAsShortArray(byte r, byte g, byte b)
            {
                return new short[]
                {
                    (short)(((r / 8) << 11) | ((g / 4) << 5) | (b / 8))

                };
            }

            byte[] Get16BitColorAsBytes(byte r, byte g, byte b)
            {
                byte[] output = new byte[2];
                Buffer.BlockCopy(Get16BitColorAsShortArray(r, g, b), 0, output, 0, 2);
                return output;
            }

            public void SendLeds(HidStream stream, byte[] leds, byte pin)
            {
                // Debug.WriteLine("Sending leds for "+pin);
                byte[] buf = new byte[PacketSize];
                buf[0] = 0;
                buf[1] = 0;
                buf[2] = pin;
                byte page = 0;
                int ld = 0;
                int nmOfLds = leds.Length / 3;

                while (ld < nmOfLds)
                {
                    buf[3] = page;
                    int ps = 4;
                    int clc = 0;
                    while (ps < 60 && ld < nmOfLds && clc < 26)
                    {
                        int ldp = ld * 3;
                        byte r = leds[ldp + 0];
                        byte g = leds[ldp + 1];
                        byte b = leds[ldp + 2];
                        var smallColor = Get16BitColorAsBytes(r, g, b);

                        buf[ps] = smallColor[0];
                        buf[ps + 1] = smallColor[1];
                        ps = ps + 2;

                        ld = ld + 1;

                        clc++;
                    }

                    SendPacket(stream, buf);

                    page++;

                }

            }

            public byte[] SetSupportedPinsCmd()
            {
                byte[] buf = new byte[3];

                buf[1] = 99;

                return buf;
            }
            public byte[] SetConfigCmd(int pin, PinConfig pc)
            {
                byte[] buf = new byte[PacketSize];
                for (int i = 0; i < PacketSize; i++)
                {
                    buf[i] = 0;
                }
                buf[0] = 0; //iunno
                buf[1] = 1; //cmd

                buf[2] = (byte)pin; //pin

                buf[3] = (byte)pc.LedCount; //leds
                buf[4] = (byte)pc.DeviceClass; //class
                for (int c = 0; c < 16; c++)
                {
                    if (c < pc.Name.Length)
                    {
                        buf[5 + c] = (byte)pc.Name.ToCharArray()[c];
                    }
                }

                return buf;
            }

            public PinConfig GetConfigFromPin(int pin, HidStream stream)
            {

                byte[] buf = new byte[PacketSize];
                buf[0] = 3;
                buf[1] = 3;
                buf[2] = (byte)pin;



                return SendPacket<PinConfig>(stream, buf);
            }

            public byte[] GetSupportedPins() => SendPacket<byte[]>(stream, SetSupportedPinsCmd());


            public string GetSerial(HidStream stream)
            {
                byte[] buf = new byte[PacketSize];
                buf[0] = 5;
                buf[1] = 5;
                buf[2] = 5;


                return SendPacket<string>(stream, buf);
            }


            public void DebugReport(byte[] ret)
            {
                Console.WriteLine("");
                for (int y = 0; y < 8; y++)
                {
                    string hx = "";
                    string ascii = "";
                    string numbers = "";
                    for (int x = 0; x < 8; x++)
                    {
                        int db = 0;
                        if (1 + x + (y * 8) < ret.Length)
                        {
                            db = ret[1 + x + (y * 8)];
                        }

                        hx = hx + $"{db:X2} ";
                        ascii = ascii + (char)(db);
                        numbers = numbers + $"{db:D2} ";
                    }

                    Console.WriteLine(hx + " - " + numbers + " - " + ascii);

                }
                Console.WriteLine("");
            }


            public class PinConfig
            {

                public int LedCount { get; set; }
                public int DeviceClass { get; set; }
                public string Name { get; set; }
                public int Pin { get; set; }
            }

            public class MadLedConfig : SLSConfigData
            {
                public PinConfig[] PinConfigs = new PinConfig[8];
            }
        }

        public MadLedMDUIViewModel ViewModel;
        public MarkdownUIBundle GetCustomConfig(ControlDevice controlDevice)
        {
            if (ViewModel == null)
            {
                ViewModel = new MadLedMDUIViewModel();
            }

            ViewModel.MadLedViewDevices = new List<MadLedMDUIViewModel.MadLedViewDevice>();

            foreach (var p in MadLedDevices)
            {
                foreach (var e in p.ControlDevices)
                {
                    ViewModel.MadLedViewDevices.Add(new MadLedMDUIViewModel.MadLedViewDevice
                    {
                        LedCount = e.LEDs.ToList().Count.ToString(),
                        Name = e.Name,
                        Serial = p.Serial
                    });
                }
            }

            string md = MarkdownReader.GetText(Assembly.GetExecutingAssembly(), "MadLedConfig.md");

            return new MarkdownUIBundle
            {
                ViewModel = ViewModel,
                Markdown = md
            };

            //return new MadLedConfigPage(this);
        }

        public bool GetIsDirty()
        {
            return false;
        }

        public void SetIsDirty(bool val)
        {

        }

        public void InvokeAdded(MadLedControlDevice mlcd)
        {
            DeviceAdded?.Invoke(this, new Events.DeviceChangeEventArgs(mlcd));
        }
    }
}
