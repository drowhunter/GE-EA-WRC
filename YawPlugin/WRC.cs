using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using YawGEAPI;
using System.Net;
using System.IO;
using System;
using System.Runtime.InteropServices;
using WRC.Properties;
using System.Collections.ObjectModel;

namespace WRC
{
    [Export(typeof(Game))]
    [ExportMetadata("Name", "EA Sports WRC")]
    [ExportMetadata("Version", "1.0")]

    public class WRC : Game
    {
        public int STEAM_ID => 1849250;
        public string PROCESS_NAME => string.Empty;
        public bool PATCH_AVAILABLE => true;
        public string AUTHOR => "DannyDan";
        public Image Logo => Resources.wrc_logo;
        public Image SmallLogo => Resources.wrc_logo;
        public Image Background => Resources.logo_wide;
        public string Description => "Run the game once BEFORE clicking the Patch Button to the right!";

        private volatile bool running = false;
        private IProfileManager controller;
        private Thread readThread;
        private UdpClient receivingUdpClient;
        private IMainFormDispatcher dispatcher;
        private IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);

        private static WRCTelemetryBytes telemetryBytes;

        public LedEffect DefaultLED() => new LedEffect((EFFECT_TYPE)1, 7, new YawColor[4]
        {
            new YawColor(255, 255, 50),
            new YawColor(80, 80, 80),
            new YawColor(255, 0, 255),
            new YawColor(255, 213, 0)
        }, 25f);

        public List<Profile_Component> DefaultProfile()
        {
            List<Profile_Component> profileComponentList = new List<Profile_Component>
            {
                new Profile_Component(9, 1, 1.0f, 1.0f, 0.0f, false, false, -1f, 1f, true, (ObservableCollection<ProfileMath>)null, (ProfileSpikeflatter)null, 0.0f, (ProfileComponentType)0, (ObservableCollection<ProfileCondition>)null),
                new Profile_Component(10, 2, 1.0f, 1.0f, 0.0f, false, false, -1f, 1f, true, (ObservableCollection<ProfileMath>)null, (ProfileSpikeflatter)null, 0.0f, (ProfileComponentType)0, (ObservableCollection<ProfileCondition>)null),
                new Profile_Component(11, 0, 1.0f, 1.0f, 0.0f, false, false, -1f, 1f, true, (ObservableCollection<ProfileMath>)null, (ProfileSpikeflatter)null, 0.0f, (ProfileComponentType)0, (ObservableCollection<ProfileCondition>)null),
                new Profile_Component(43, 1, 1.0f, 1.0f, 0.0f, false, false, -1f, 1f, true, (ObservableCollection<ProfileMath>)null, (ProfileSpikeflatter)null, 0.0f, (ProfileComponentType)0, (ObservableCollection<ProfileCondition>)null),
                new Profile_Component(44, 1, 1.0f, 1.0f, 0.0f, false, true, -1f, 1f, true, (ObservableCollection<ProfileMath>)null, (ProfileSpikeflatter)null, 0.0f, (ProfileComponentType)0, (ObservableCollection<ProfileCondition>)null)
                //new Profile_Component(4, 3, 25.0f, 25.0f, 0.0f, false, false, -1f, 1f, true, (ObservableCollection<ProfileMath>)null, (ProfileSpikeflatter)null, 0.0f, (ProfileComponentType)0, (ObservableCollection<ProfileCondition>)null),
                //new Profile_Component(4, 4, 25.0f, 25.0f, 0.0f, false, false, -1f, 1f, true, (ObservableCollection<ProfileMath>)null, (ProfileSpikeflatter)null, 0.0f, (ProfileComponentType)0, (ObservableCollection<ProfileCondition>)null)
            };
            
            return profileComponentList;
        }

        public void Exit()
        {
            receivingUdpClient.Close();
            receivingUdpClient = null;
            running = false;
        }

        public Image GetBackground()
        {
            return Background;
        }

        public string GetDescription()
        {
            return Description;
        }

        public string[] GetInputData()
        {
            Type t = typeof(WRCTelemetryBytes);
            FieldInfo[] fields = t.GetFields();

            string[] inputs = new string[fields.Length];

            for (int i = 0; i<fields.Length; i++)
            {
                inputs[i] = fields[i].Name;
            }
            return inputs;
        }

        public Image GetLogo()
        {
            return Logo;
        }

        public void Init()
        {
            running = true;
            receivingUdpClient = new UdpClient(20999);
            readThread = new Thread(new ThreadStart(ReadFunction));
            readThread.Start();
        }
        private void ReadFunction()
        {
            /*using (StreamWriter sw = File.AppendText(@"D:\1\DL\output.txt"))
            {
                sw.WriteLine(DateTime.Now.ToString("HH:mm:ss tt") + ": " + running);
            }*/

            FieldInfo[] fields = typeof(WRCTelemetryBytes).GetFields();

            while (running)
            {
                try
                {
                    var timeToWait = TimeSpan.FromSeconds(2);

                    var asyncResult = receivingUdpClient.BeginReceive(null, null);
                    asyncResult.AsyncWaitHandle.WaitOne(timeToWait);
                    if (asyncResult.IsCompleted)
                    {
                        try
                        {
                            byte[] buffer = receivingUdpClient.EndReceive(asyncResult, ref RemoteIpEndPoint);

                            /*unsafe
                            {
                                int test = sizeof(WRCTelemetryBytes);                                
                            }*/

                            //var size = Marshal.SizeOf(buffer);
                            var size = buffer.Length;
                            var ptr = IntPtr.Zero;
                            try
                            {
                                ptr = Marshal.AllocHGlobal(size);
                                Marshal.Copy(buffer, 0, ptr, size);
                                telemetryBytes = (WRCTelemetryBytes)Marshal.PtrToStructure(ptr, telemetryBytes.GetType());
                            }
                            finally
                            {
                                Marshal.FreeHGlobal(ptr);
                            }
                                                        
                            for (int i = 0; i < fields.Length; i++)
                            {
                                controller.SetInput(i, Convert.ToSingle(fields[i].GetValue(telemetryBytes)));
                            }
                        }
                        catch (Exception ex)
                        {
                            /*using (StreamWriter sw = File.AppendText(@"D:\1\DL\output.txt"))
                            {
                                sw.WriteLine(ex);
                            }*/

                            for (int i = 0; i < fields.Length; i++)
                            {
                                controller.SetInput(i, 0);
                            }
                        }
                    }
                    else
                    {
                        for (int i = 0; i < fields.Length; i++)
                        {
                            controller.SetInput(i, 0);
                        }
                    }                    
                }
                catch (Exception ex)
                {
                    running = false;

                    /*using (StreamWriter sw = File.AppendText(@"D:\1\DL\output.txt"))
                    {
                        sw.WriteLine(ex);
                    }*/

                    for (int i = 0; i < fields.Length; i++)
                    {
                        controller.SetInput(i, 0);
                    }
                }
            }

            while (!running)
            {
                for (int i = 0; i < fields.Length; i++)
                {
                    controller.SetInput(i, 0);
                }
            }
        }

        public void PatchGame()
        {
            File.WriteAllText(@Environment.GetEnvironmentVariable("userprofile") + "\\Documents\\My Games\\WRC\\telemetry\\config.json", string.Empty);
            using (var sw = new StreamWriter(@Environment.GetEnvironmentVariable("userprofile") + "\\Documents\\My Games\\WRC\\telemetry\\config.json", true))
            {
                sw.WriteLine("{\r\n    \"schema\": 2,\r\n    \"udp\": {\r\n        \"packets\": [\r\n            {\r\n                \"structure\": \"wrc\",\r\n                \"packet\": \"session_update\",\r\n                \"ip\": \"127.0.0.1\",\r\n                \"port\": 20777,\r\n                \"frequencyHz\": -1,\r\n                \"bEnabled\": false\r\n            },\r\n            {\r\n                \"structure\": \"wrc_experimental\",\r\n                \"packet\": \"session_start\",\r\n                \"ip\": \"127.0.0.1\",\r\n                \"port\": 20778,\r\n                \"frequencyHz\": 0,\r\n                \"bEnabled\": false\r\n            },\r\n            {\r\n                \"structure\": \"wrc_experimental\",\r\n                \"packet\": \"session_update\",\r\n                \"ip\": \"127.0.0.1\",\r\n                \"port\": 20777,\r\n                \"frequencyHz\": -1,\r\n                \"bEnabled\": false\r\n            },\r\n            {\r\n                \"structure\": \"wrc_experimental\",\r\n                \"packet\": \"session_end\",\r\n                \"ip\": \"127.0.0.1\",\r\n                \"port\": 20778,\r\n                \"frequencyHz\": 0,\r\n                \"bEnabled\": false\r\n            },\r\n            {\r\n                \"structure\": \"wrc_experimental\",\r\n                \"packet\": \"session_pause\",\r\n                \"ip\": \"127.0.0.1\",\r\n                \"port\": 20778,\r\n                \"frequencyHz\": 0,\r\n                \"bEnabled\": false\r\n            },\r\n            {\r\n                \"structure\": \"wrc_experimental\",\r\n                \"packet\": \"session_resume\",\r\n                \"ip\": \"127.0.0.1\",\r\n                \"port\": 20778,\r\n                \"frequencyHz\": 0,\r\n                \"bEnabled\": false\r\n            },\r\n            {\r\n                \"structure\": \"custom1\",\r\n                \"packet\": \"session_update\",\r\n                \"ip\": \"127.0.0.1\",\r\n                \"port\": 20777,\r\n                \"frequencyHz\": -1,\r\n                \"bEnabled\": false\r\n            },\r\n\t\t\t{\r\n                \"structure\": \"wrc_yaw_ge\",\r\n                \"packet\": \"session_update\",\r\n                \"ip\": \"127.0.0.1\",\r\n                \"port\": 20999,\r\n                \"frequencyHz\": -1,\r\n                \"bEnabled\": true\r\n            }\r\n        ]\r\n    },\r\n    \"lcd\": {\r\n        \"bDisplayGears\": true\r\n    },\r\n    \"dBox\": {\r\n        \"bEnabled\": true\r\n    }\r\n}");
            }

            File.WriteAllText(@Environment.GetEnvironmentVariable("userprofile") + "\\Documents\\My Games\\WRC\\telemetry\\udp\\wrc_yaw_ge.json", string.Empty);
            using (var sw = new StreamWriter(@Environment.GetEnvironmentVariable("userprofile") + "\\Documents\\My Games\\WRC\\telemetry\\udp\\wrc_yaw_ge.json", true))
            {
                sw.WriteLine("{\r\n\t\"versions\":\r\n\t{\r\n\t\t\"schema\": 1,\r\n\t\t\"data\": 3\r\n\t},\r\n\t\"id\": \"wrc_yaw_ge\",\r\n\t\"header\":\r\n\t{\r\n\t\t\"channels\": []\r\n\t},\r\n\t\"packets\": [\r\n\t\t{\r\n\t\t\t\"id\": \"session_update\",\r\n\t\t\t\"channels\": [\r\n\t\t\t\t\"vehicle_gear_index\",\r\n\t\t\t\t\"vehicle_gear_index_neutral\",\r\n\t\t\t\t\"vehicle_gear_index_reverse\",\r\n\t\t\t\t\"vehicle_gear_maximum\",\r\n\t\t\t\t\"vehicle_speed\",\r\n\t\t\t\t\"vehicle_transmission_speed\",\r\n\t\t\t\t\"vehicle_position_x\",\r\n\t\t\t\t\"vehicle_position_y\",\r\n\t\t\t\t\"vehicle_position_z\",\r\n\t\t\t\t\"vehicle_velocity_x\",\r\n\t\t\t\t\"vehicle_velocity_y\",\r\n\t\t\t\t\"vehicle_velocity_z\",\r\n\t\t\t\t\"vehicle_acceleration_x\",\r\n\t\t\t\t\"vehicle_acceleration_y\",\r\n\t\t\t\t\"vehicle_acceleration_z\",\r\n\t\t\t\t\"vehicle_left_direction_x\",\r\n\t\t\t\t\"vehicle_left_direction_y\",\r\n\t\t\t\t\"vehicle_left_direction_z\",\r\n\t\t\t\t\"vehicle_forward_direction_x\",\r\n\t\t\t\t\"vehicle_forward_direction_y\",\r\n\t\t\t\t\"vehicle_forward_direction_z\",\r\n\t\t\t\t\"vehicle_up_direction_x\",\r\n\t\t\t\t\"vehicle_up_direction_y\",\r\n\t\t\t\t\"vehicle_up_direction_z\",\r\n\t\t\t\t\"vehicle_hub_position_bl\",\r\n\t\t\t\t\"vehicle_hub_position_br\",\r\n\t\t\t\t\"vehicle_hub_position_fl\",\r\n\t\t\t\t\"vehicle_hub_position_fr\",\r\n\t\t\t\t\"vehicle_hub_velocity_bl\",\r\n\t\t\t\t\"vehicle_hub_velocity_br\",\r\n\t\t\t\t\"vehicle_hub_velocity_fl\",\r\n\t\t\t\t\"vehicle_hub_velocity_fr\",\r\n\t\t\t\t\"vehicle_cp_forward_speed_bl\",\r\n\t\t\t\t\"vehicle_cp_forward_speed_br\",\r\n\t\t\t\t\"vehicle_cp_forward_speed_fl\",\r\n\t\t\t\t\"vehicle_cp_forward_speed_fr\",\r\n\t\t\t\t\"vehicle_brake_temperature_bl\",\r\n\t\t\t\t\"vehicle_brake_temperature_br\",\r\n\t\t\t\t\"vehicle_brake_temperature_fl\",\r\n\t\t\t\t\"vehicle_brake_temperature_fr\",\r\n\t\t\t\t\"vehicle_engine_rpm_max\",\r\n\t\t\t\t\"vehicle_engine_rpm_idle\",\r\n\t\t\t\t\"vehicle_engine_rpm_current\",\r\n\t\t\t\t\"vehicle_throttle\",\r\n\t\t\t\t\"vehicle_brake\",\r\n\t\t\t\t\"vehicle_clutch\",\r\n\t\t\t\t\"vehicle_steering\",\r\n\t\t\t\t\"vehicle_handbrake\",\r\n\t\t\t\t\"stage_current_time\",\r\n\t\t\t\t\"stage_current_distance\",\r\n\t\t\t\t\"stage_length\"\r\n\t\t\t]\r\n\t\t}\r\n\t]\r\n}");
            }

            return;
        }

        public void SetReferences(IProfileManager controller, IMainFormDispatcher dispatcher)
        {
            this.controller = controller;
            this.dispatcher = dispatcher;
        }

        public Dictionary<string, ParameterInfo[]> GetFeatures()
        {
            return null;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct WRCTelemetryBytes
    {
        public byte vehicle_gear_index;
        public byte vehicle_gear_index_neutral;
        public byte vehicle_gear_index_reverse;
        public byte vehicle_gear_maximum;
        public float vehicle_speed;
        public float vehicle_transmission_speed;
        public float vehicle_position_x;
        public float vehicle_position_y;
        public float vehicle_position_z;
        public float vehicle_velocity_x;
        public float vehicle_velocity_y;
        public float vehicle_velocity_z;
        public float vehicle_acceleration_x;
        public float vehicle_acceleration_y;
        public float vehicle_acceleration_z;
        public float vehicle_left_direction_x;
        public float vehicle_left_direction_y;
        public float vehicle_left_direction_z;
        public float vehicle_forward_direction_x;
        public float vehicle_forward_direction_y;
        public float vehicle_forward_direction_z;
        public float vehicle_up_direction_x;
        public float vehicle_up_direction_y;
        public float vehicle_up_direction_z;
        public float vehicle_hub_position_bl;
        public float vehicle_hub_position_br;
        public float vehicle_hub_position_fl;
        public float vehicle_hub_position_fr;
        public float vehicle_hub_velocity_bl;
        public float vehicle_hub_velocity_br;
        public float vehicle_hub_velocity_fl;
        public float vehicle_hub_velocity_fr;
        public float vehicle_cp_forward_speed_bl;
        public float vehicle_cp_forward_speed_br;
        public float vehicle_cp_forward_speed_fl;
        public float vehicle_cp_forward_speed_fr;
        public float vehicle_brake_temperature_bl;
        public float vehicle_brake_temperature_br;
        public float vehicle_brake_temperature_fl;
        public float vehicle_brake_temperature_fr;
        public float vehicle_engine_rpm_max;
        public float vehicle_engine_rpm_idle;
        public float vehicle_engine_rpm_current;
        public float vehicle_throttle;
        public float vehicle_brake;
        public float vehicle_clutch;
        public float vehicle_steering;
        public float vehicle_handbrake;
        public float stage_current_time;
        public double stage_current_distance;
        public double stage_length;
    }
}
