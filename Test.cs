using System;
using UnityEngine;
using System.IO.Ports;
using System.Threading;
using System.Collections;

/*
https://forum.kerbalspaceprogram.com/index.php?/topic/153765-getting-started-the-basics-of-writing-a-plug-in/
https://wiki.kerbalspaceprogram.com/wiki/Category:Community_API_Documentation
https://www.kerbalspaceprogram.com/api/annotated.html
*/

namespace KerbalControl
{
    struct Data
    {
        public bool switch1;
        public bool switch2;
        public bool switch3;
        public bool switch4;
        public bool key;
        public bool keyBtn;

        public int rot6;
        public int rot12;

        public bool btnUL;
        public bool btnUR;
        public bool btnBL;
        public bool btnBR;
        public int lcdPot;
    }


    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class Test : MonoBehaviour
    {
        SerialPort serial;
        Thread sendThread;
        Thread getThread;
        Vessel ship;

        bool stageCharged = false;
        bool lastSwitchKey = false;
        int lastTime = 0;
        Data data = new Data();
        // this is the index we're expecting next, -1 is no clue
        int serialIndex = -1;

        string bitArrayToString(BitArray bitArray)
        {
            string str = "";
            foreach (bool bit in bitArray)
            {
                str += (bit ? 1 : 0).ToString();
            }

            return str;
        }

        void GetData()
        {
            while (true)
            {
                int serialByte;

                try
                {
                    serialByte = serial.ReadByte();
                    Debug.Log(Time.time);
                }
                catch (TimeoutException)
                {
                    continue;
                }

                // If we don't know where we are in the stream
                if(serialIndex == -1)
                {
                    // If we see the start byte
                    if (serialByte == 0b00001010)
                    {
                        // we expect the 0th (first) byte next
                        serialIndex = 0;
                    }

                    continue;
                }

                BitArray bitArray = new BitArray(new byte[] { (byte)serialByte });
                bool[] bits = new bool[bitArray.Count];
                bitArray.CopyTo(bits, 0);
                Array.Reverse(bits);

                if (serialIndex == 0)
                {
                    data.switch1 = bits[2];
                    data.switch2 = bits[3];
                    data.switch3 = bits[4];
                    data.switch4 = bits[5];

                    data.key = bits[6];
                    data.keyBtn = bits[7];

                    serialIndex = 1;
                }
                else if (serialIndex == 1)
                {
                    data.rot6 = (4 * (bits[1] ? 1 : 0)) + (2 * (bits[2] ? 1 : 0)) + (1 * (bits[3] ? 1 : 0));

                    data.rot12 = (8 * (bits[4] ? 1 : 0)) + (4 * (bits[5] ? 1 : 0)) + (2 * (bits[6] ? 1 : 0)) + (1 * (bits[7] ? 1 : 0));

                    serialIndex = 2;
                }
                else if (serialIndex == 2)
                {
                    data.btnUL = bits[1];
                    data.btnUR = bits[2];
                    data.btnBL = bits[3];
                    data.btnBR = bits[4];

                    data.lcdPot = (4 * (bits[5] ? 1 : 0)) + (2 * (bits[6] ? 1 : 0)) + (1 * (bits[7] ? 1 : 0));

                    serialIndex = -1;
                }
            }
        }

        void SendData()
        {
            while (true)
            {
                int altitude = (int)ship.altitude;

                int[] displayValues = { altitude };
                String str = "";
                foreach (int value in displayValues)
                {
                    str += value.ToString().PadLeft(8, '0');
                }

                serial.WriteLine(str);


                //Thread.Sleep(50);
            }
        }

        IEnumerator Setup()
        {
            yield return new WaitForSeconds(5);
            serial = new SerialPort();
            serial.PortName = "/dev/ttyACM1";
            serial.BaudRate = 115200;
            serial.ReadTimeout = 100;
            serial.WriteTimeout = 100;
            serial.Open();

            if (serial.IsOpen)
            {
                yield return new WaitForSeconds(5);

                sendThread = new Thread(new ThreadStart(SendData));
                sendThread.Start();

                getThread = new Thread(new ThreadStart(GetData));
                getThread.Start();

                Debug.Log("Port is open");
            }

            yield return null;
        }
        
        public void Start()
        {
            StartCoroutine("Setup");

            ship = FlightGlobals.ActiveVessel;
        }
        
        public void Update()
        {
            if(lastTime != data.lcdPot)
            {
                TimeWarp.SetRate(data.lcdPot, false);
                lastTime = data.lcdPot;
            }

            
            if (data.rot12 == 0)
            {
                ship.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
            }
            else
            {
                ship.ActionGroups.SetGroup(KSPActionGroup.SAS, true);
                VesselAutopilot.AutopilotMode[] sasModes = { VesselAutopilot.AutopilotMode.StabilityAssist,
                                                             VesselAutopilot.AutopilotMode.Maneuver,
                                                             VesselAutopilot.AutopilotMode.Prograde,
                                                             VesselAutopilot.AutopilotMode.Retrograde,
                                                             VesselAutopilot.AutopilotMode.Normal,
                                                             VesselAutopilot.AutopilotMode.Antinormal,
                                                             VesselAutopilot.AutopilotMode.RadialIn,
                                                             VesselAutopilot.AutopilotMode.RadialOut,
                                                             VesselAutopilot.AutopilotMode.Target,
                                                             VesselAutopilot.AutopilotMode.AntiTarget};
                if (ship.Autopilot.CanSetMode(sasModes[data.rot12 - 1]))
                {
                    ship.Autopilot.SetMode(sasModes[data.rot12 - 1]);
                }
            }

            if (data.key != lastSwitchKey)
            {
                stageCharged = data.key;

                lastSwitchKey = data.key;
            }

            if (data.keyBtn && stageCharged)
            {
                KSP.UI.Screens.StageManager.ActivateNextStage();

                stageCharged = false;
            }

            ship.ActionGroups.SetGroup(KSPActionGroup.Light, data.switch1);
            ship.ActionGroups.SetGroup(KSPActionGroup.Gear, data.switch2);
            ship.ActionGroups.SetGroup(KSPActionGroup.Brakes, data.switch3);
            ship.ActionGroups.SetGroup(KSPActionGroup.RCS, data.switch4);
            
        }

        public void OnDestroy()
        {
            serial.Close();
            sendThread.Abort();
            getThread.Abort();
        }
    }
}
