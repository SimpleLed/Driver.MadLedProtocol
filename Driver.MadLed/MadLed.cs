using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Controls;
using HidSharp;
using SimpleLed;

namespace Driver.MadLed
{
    public class MadLed : ISimpleLedWithConfig
    {
        public void Dispose()
        {

        }

        private MadLedDevice.MadLedConfig config;
        private List<MadLedControlDevice> controlDevices = new List<MadLedControlDevice>();
        public event Events.DeviceChangeEventHandler DeviceAdded;
        public event Events.DeviceChangeEventHandler DeviceRemoved;

        private List<USBDevice> SupportedDevices = new List<USBDevice>
        {
            new USBDevice{VID = 0x1b4f, HID = 0x9207}
        };

        public void Configure(DriverDetails driverDetails)
        {


            config = new MadLedDevice.MadLedConfig();
            config.PinConfigs = new MadLedDevice.PinConfig[8];
            config.PinConfigs[2] = new MadLedDevice.PinConfig
            {
                DeviceClass = 1,
                LedCount = 19,
                Name = "LED Strip"
            };

            foreach (USBDevice supportedDevice in SupportedDevices)
            {
                InterestedUSBChange(supportedDevice.VID, supportedDevice.HID.Value, true);
            }


        }

        public void Push(ControlDevice controlDevice)
        {
            MadLedControlDevice mlcd = (MadLedControlDevice)controlDevice;

            byte[] leds = new byte[mlcd.LEDs.Length * 3];

            for (int i = 0; i < mlcd.LEDs.Length; i++)
            {

                leds[(i * 3) + 0] = (byte)mlcd.LEDs[i].Color.Blue;
                leds[(i * 3) + 1] = (byte)mlcd.LEDs[i].Color.Green;
                leds[(i * 3) + 2] = (byte)mlcd.LEDs[i].Color.Red;
            }

            mlcd.MadLedDevice.SendLeds(mlcd.MadLedDevice.stream, leds, (byte)mlcd.Pin);
            mlcd.MadLedDevice.PresentLeds(mlcd.MadLedDevice.stream);
        }

        public void Pull(ControlDevice controlDevice)
        {
            //throw new NotImplementedException();
        }

        public DriverProperties GetProperties()
        {
            return new DriverProperties
            {
                SupportsPull = false,
                SupportsPush = true,
                IsSource = false,
                SupportsCustomConfig = true,
                Id = Guid.Parse("5dcf474c-4bd0-4516-b871-f98ca885dbb0"),
                Author = "MadNinja",
                Blurb = "Driver for controlling MadLed controllers.",
                CurrentVersion = new ReleaseNumber(1, 0, 0, 6),
                GitHubLink = "https://github.com/SimpleLed/Driver.MadLedProtocol",
                IsPublicRelease = false,
                SupportedDevices = this.SupportedDevices
            };
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
                var mld = new MadLedDevice(VID, PID);

                if (mld.Success)
                {
                    ConnectedMadLedUnits.Add(mld.Serial);

                    var remove = MadLedDevices.Where(x => x.Serial == mld.Serial);
                    foreach (MadLedDevice madLedDevice in remove)
                    {
                        MadLedDevices.Remove(madLedDevice);
                    }

                    MadLedDevices.Add(mld);
                    for (int i = 0; i < 8; i++)
                    {
                        var pc = mld.GetConfigFromPin(i + 1, mld.stream);

                        if (pc != null)
                        {
                            var pcfg = mld.SetConfigCmd(i + 1, pc);
                            pcfg[1] = 10;
                            //var pc = config.PinConfigs[i];
                            mld.SendPacket(mld.stream, pcfg);
                            mld.ReadReturnReport(mld.stream);


                            MadLedControlDevice mlcd = new MadLedControlDevice
                            {
                                ConnectedTo = "Channel " + (i + 1),
                                DeviceType = deviceTypes[pc.DeviceClass],
                                Name = pc.Name,
                                MadLedDevice = mld,
                                LEDs = new ControlDevice.LedUnit[pc.LedCount],
                                Driver = this,
                                Pin = i + 1
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

                            DeviceAdded?.Invoke(this, new Events.DeviceChangeEventArgs(mlcd));

                        }
                    }


                    //mld.SendPacket(mld.stream, mld.SetConfigCmd(2, config.PinConfigs[2]));
                    return;
                }
            }
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
            public HidStream stream = null;
            public bool Success;
            public int VID;
            public int PID;
            public string Serial;
            public MadLedDevice(int vid, int pid)
            {
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
                bool success = false;


                while (attempts < 10 && !success)
                {
                    try
                    {
                        Console.WriteLine("Attempting connection");
                        device = devices[attempts % devices.Count()];
                        byte[] t = device.GetRawReportDescriptor();
                        Console.WriteLine(device.GetFriendlyName());

                        stream = device.Open(terp);
                        SetCalibration(stream);
                        Console.WriteLine("ooh, somting Happened!");
                        string serial = GetSerial(stream);
                        if (serial == null)
                        {
                            Console.WriteLine("device not correct");
                            stream = null;
                            return;
                        }

                        Serial = serial.Substring(4);

                        Success = true;
                        //SendPacket(stream,0x60, 0);
                        success = true;
                        VID = vid;
                        PID = pid;


                    }
                    catch (Exception ep)
                    {
                        Console.WriteLine(ep.Message);
                        attempts++;
                        device = null;
                        Thread.Sleep(100);
                    }
                }

                if (device == null)
                {
                    Console.WriteLine("no device found");
                    stream = null;
                    return;
                }
            }

            public void SendPacket(HidStream stream, byte[] packet)
            {
                stream.Write(packet);
                //stream.SetFeature(packet);
            }

            public void PresentLeds(HidStream stream)
            {
                byte[] bufterd = new byte[63];
                bufterd[0] = 3;
                bufterd[1] = 3;
                bufterd[2] = 3;
                stream.Write(bufterd);

            }

            public void SendLeds(HidStream stream, byte[] leds, byte pin)
            {
                byte[] buf = new byte[63];
                buf[0] = 0;
                buf[1] = 1;
                buf[2] = pin;
                byte page = 0;
                int ld = 0;
                int nmOfLds = leds.Length / 3;
                int ldos = 0;
                while (ld < nmOfLds)
                {
                    buf[3] = page;
                    int ps = 4;

                    while (ld - ldos < 19 && ld < nmOfLds)
                    {

                        int ldp = ld * 3;
                        byte r = leds[ldp + 0];
                        byte g = leds[ldp + 1];
                        byte b = leds[ldp + 2];
                        buf[ps] = g;
                        buf[ps + 1] = b;
                        buf[ps + 2] = r;
                        ps = ps + 3;


                        ld = ld + 1;
                    }

                    ldos = ld;

                    // DebugReport(buf);
                    stream.Write(buf);

                    page++;

                }

            }

            public byte[] SetConfigCmd(int pin, PinConfig pc)
            {
                byte[] buf = new byte[65];
                for (int i = 0; i < 65; i++)
                {
                    buf[i] = 0;
                }
                buf[0] = 0; //iunno
                buf[1] = 0; //cmd

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

            public MadLedDevice.PinConfig GetConfigFromPin(int pin, HidStream stream)
            {
                byte[] buf = new byte[63];
                buf[0] = 2;
                buf[1] = 2;
                buf[2] = (byte)pin;


                SendPacket(stream, buf);

                byte[] ret = new byte[63];

                stream.Read(ret);
                if (ret[1] != 128)
                {
                    return null;
                }

                var pc = new PinConfig();

                pc.Pin = ret[2];
                pc.LedCount = ret[3];
                pc.DeviceClass = ret[4];

                for (int i = 5; i < 5 + 16; i++)
                {
                    byte t = ret[i];

                    if (t == 13 || t == 0)
                    {
                        break;
                    }
                    else
                    {
                        pc.Name = pc.Name + (char)t;
                    }
                }

                return pc;

            }

            public string GetSerial(HidStream stream)
            {
                byte[] buf = new byte[63];
                buf[0] = 5;
                buf[1] = 5;
                buf[2] = 5;


                SendPacket(stream, buf);

                byte[] ret = new byte[63];

                string r = "";
                stream.Read(ret);
                if (ret[0] != 0)
                {
                    return null;
                }
                for (int i = 1; i < 1 + 16; i++)
                {
                    byte t = ret[i];

                    if (t == 13 || t == 0)
                    {
                        break;
                    }
                    else
                    {
                        r = r + (char)t;
                    }
                }

                if (!r.StartsWith("MLNG"))
                {
                    return null;
                }

                return r;
            }

            public void ReadReturnReport(HidStream stream)
            {
                byte[] ret = new byte[64];

                stream.Read(ret);

                DebugReport(ret);
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

            public void SetCalibration(HidStream stream)
            {
                byte[] buffer = new byte[64];
                buffer[0] = 0xcc;
                buffer[1] = 0x33;

                // D_LED1 WS2812 GRB, 0x00RRGGBB to 0x00GGRRBB
                buffer[2] = 0x02; // B
                buffer[3] = 0x00; // G
                buffer[4] = 0x01; // R
                buffer[5] = 0x00;

                // D_LED2 WS2812 GRB
                buffer[6] = 0x02;
                buffer[7] = 0x00;
                buffer[8] = 0x01;
                buffer[9] = 0x00;

                // LED C1/C2 12vGRB, seems pins already connect to LEDs correctly
                buffer[10] = 0x00;
                buffer[11] = 0x01;
                buffer[12] = 0x02;
                buffer[13] = 0x00;

                // Spare set seen in some Motherboard models
                buffer[14] = 0x00;
                buffer[15] = 0x01;
                buffer[16] = 0x02;
                buffer[17] = 0x00;

                SendPacket(stream, buffer);
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

        public UserControl GetCustomConfig(ControlDevice controlDevice)
        {
            return new MadLedConfigPage(this);
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
