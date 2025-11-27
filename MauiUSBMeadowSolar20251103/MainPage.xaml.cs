using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.IO;
using System.Globalization;

using FTD2XX_NET;

namespace MauiUSBMeadowSolar20251103
{
    public partial class MainPage : ContentPage
    {
        
        //https://ftdichip.com/software-examples/code-examples/csharp-examples

        UInt32 ftdiDeviceCount = 0;                  // how many FTDI devices are plugged in
        FTDI.FT_STATUS ftStatus = FTDI.FT_STATUS.FT_OK; // this holds the result of FTDI functions

        // make an FTDI device object so we can talk to the USB thing
        FTDI myFtdiDevice = new FTDI();

        // this is an array to store info about up to 64 FTDI devices
        FTDI.FT_DEVICE_INFO_NODE[] ftdiDeviceList = new FTDI.FT_DEVICE_INFO_NODE[64];
        private uint packetSize = 39;     // how many bytes we expect in each packet from the board
        private uint numBytesWritten;     // how many bytes we actually wrote out
        private bool bPortOpen = false;   // flag to remember if port is open or not
        private bool sendDataOk;          // flag we can use for send
        private string newPacket;         // the latest packet we read from USB
        //private int newPacketNumber;    // old variable not used
        private int oldPackNumber = -1;   // last packet number we saw, start at -1 so first one always passes
        private int newPacketNumber = 0;  // current packet number from the message
        private int lostPacketCount = 0;  // counter if packets are missing
        private int packetRollover = 0;   // counter when packet number goes 999 → 000
        private int chkSumError = 0;      // counter for bad checksum packets

        // this was for serial port before, now using FTDI
        StringBuilder stringBuilderSend = new StringBuilder("###1111196"); // starting message we send, we edit bits in here

        SolarCalc solarCalc = new SolarCalc(); // our other class that converts packet into voltages and currents

        SolarGraphData graphData = new SolarGraphData();   // holds points for the graph

        // File Unity will read to know plant growth (0–100%)
        private const string SolarPercentFilePath = @"C:\Temp\solar_growth.txt";

        // Max solar voltage (in volts) that maps to 100% growth in the game
        private const double MaxSolarVoltageForGame = 3.3;

        public MainPage()
        {
            InitializeComponent(); // builds the UI from XAML
        }

        private void btnOpenClose_Clicked(object sender, EventArgs e)
        {
            // if port is NOT open, then open it
            if (!bPortOpen)
            {
                RefreshDeviceList(); // get list of USB/FTDI devices
                sendDataOk = false; // reset flag
                SetupFTDIdeviceByAutoSerialNumber(); // open the first device
                _ = MonitorUSBdata(); // start the loop that keeps reading data
                btnOpenClose.Text = "Close"; // change button text
                bPortOpen = true; // remember that we opened it
            }
            else // if it WAS open, then close it
            {
                CloseFtdiDevice(); // close it
                btnOpenClose.Text = "Open"; // change button text back
                bPortOpen = false; // remember it is closed
            }
        }

        private void CloseFtdiDevice()
        {
            ftStatus = myFtdiDevice.Close(); // tell FTDI to close
            return; // done
        }

        private async Task MonitorUSBdata()
        {
            // keep doing this as long as the FTDI device is open
            while (myFtdiDevice.IsOpen)
            {
                // we want to read only when a whole packet is there
                UInt32 numBytesAvailable = 0;
                do
                {
                    // ask FTDI how many bytes are ready
                    ftStatus = myFtdiDevice.GetRxBytesAvailable(ref numBytesAvailable);
                    if (ftStatus != FTDI.FT_STATUS.FT_OK)
                    {
                        // if something went wrong, write it to debug and stop
                        Trace.WriteLine("Failed to get number of bytes available to read (error " + ftStatus.ToString() + ")");
                        return;
                    }
                } while (numBytesAvailable < packetSize); // keep looping until we have a full packet

                // now we have enough to read
                string readData;
                UInt32 numBytesRead = 0;
                // read the data as a string
                ftStatus = myFtdiDevice.Read(out readData, numBytesAvailable, ref numBytesRead);
                if (ftStatus != FTDI.FT_STATUS.FT_OK)
                {
                    Trace.WriteLine("Failed to read data (error " + ftStatus.ToString() + ")");
                    return;
                }

                // show what we read in debug
                Trace.WriteLine("Read data: " + readData);
                newPacket = readData; // save it so we can parse it

                // if user wants to keep history, add to front
                if (checkBoxHistory.IsChecked)
                {
                    LabelRXdata.Text = newPacket + LabelRXdata.Text;
                }
                else // otherwise just show last packet
                {
                    LabelRXdata.Text = newPacket;
                }

                await Task.Delay(90); // small pause so UI can update

                // only parse if packet is big enough
                if (newPacket.Length >= packetSize)
                {
                    ParseNewPacket();
                }

            }
        }

        private void ParseNewPacket()
        {
            // make sure packet is not empty
            if (string.IsNullOrEmpty(newPacket))
                return;
            // make sure it's long enough
            if (newPacket.Length < 37)
                return;
            // make sure it starts with ### like our format
            if (!newPacket.StartsWith("###"))
                return;

            // 1) do checksum to make sure packet is good
            int calcChk = 0;
            for (int i = 3; i < 34; i++) // go through the part of packet we check
            {
                calcChk += (byte)newPacket[i];
            }
            calcChk %= 1000; // keep last 3 digits

            // get the checksum that came in the packet (chars 34,35,36)
            int recvChk = Convert.ToInt32(newPacket.Substring(34, 3));

            // if checksum we made does not match packet checksum, count error and stop
            if (calcChk != recvChk)
            {
                chkSumError++;
                Trace.WriteLine("Checksum error");
                return;
            }

            // 2) check packet number so we can tell if we lost some
            newPacketNumber = Convert.ToInt32(newPacket.Substring(3, 3)); // packet number is after ###

            if (oldPackNumber > -1) // only do this after first packet
            {
                if (newPacketNumber < oldPackNumber)
                {
                    // this means we wrapped around 999 → 000
                    packetRollover++;
                    if (oldPackNumber != 999)
                    {
                        // count how many we missed across the rollover
                        lostPacketCount += 999 - oldPackNumber + newPacketNumber;
                    }
                }
                else
                {
                    // normal case: check if we skipped numbers
                    if (newPacketNumber != oldPackNumber + 1)
                    {
                        // add how many were skipped
                        lostPacketCount += newPacketNumber - oldPackNumber - 1;
                    }
                }
            }

            // remember this packet number for next time
            oldPackNumber = newPacketNumber;

            // 3) make a nice string that shows all parts of the packet
            string parsedData =
                $"{newPacket.Length,-14}" +
                $"{newPacket.Substring(0, 3),-14}" +
                $"{newPacket.Substring(3, 3),-14}" +
                $"{newPacket.Substring(6, 4),-14}" +
                $"{newPacket.Substring(10, 4),-14}" +
                $"{newPacket.Substring(14, 4),-14}" +
                $"{newPacket.Substring(18, 4),-14}" +
                $"{newPacket.Substring(22, 4),-14}" +
                $"{newPacket.Substring(26, 4),-14}" +
                $"{newPacket.Substring(30, 4),-14}" +
                $"{newPacket.Substring(34, 3),-14}" +
                $"{calcChk,-14}" +
                $"{lostPacketCount,-14}" +
                $"{chkSumError,-14}" +
                $"{packetRollover}\r\n";

            // show it either with history or replace
            if (checkBoxParsedHistory.IsChecked)
                LabelParsedData.Text = parsedData + LabelParsedData.Text;
            else
                LabelParsedData.Text = parsedData;

            // 4) update the labels that show solar stuff
            DisplaySolarData(newPacket);
        }

        private void DisplaySolarData(string validPacket)
        {
            // we update UI things on the UI thread
            Dispatcher.Dispatch(() =>
            {
                // let the solarCalc break the packet into numbers
                solarCalc.ParseSolarData(validPacket);

                // 1) get numeric voltages for graph + progress bar
                double solarV = solarCalc.SolarVoltageVolts;
                double batteryV = solarCalc.BatteryVoltageVolts;

                // 2) update the UI labels (what you already had)
                labelSolarVolt.Text = solarCalc.FormatVoltage(solarCalc.AnalogVoltage[0]);
                labelBatteryVolt.Text = solarCalc.FormatVoltage(solarCalc.AnalogVoltage[2]);
                labelBatteryCurrent.Text = solarCalc.GetBatteryCurrentText();
                labelLED1Current.Text = solarCalc.GetLed1CurrentText();
                labelLED2Current.Text = solarCalc.GetLed2CurrentText();

                // 3) update growth bar to match Unity
                double percent = Math.Clamp(solarV / 3.3, 0.0, 1.0); // 3.3V = 100%
                progressPlantGrowth.Progress = percent;

                // 4) store a sample for the graph
                graphData.AddSample(solarV, batteryV);

                // fill in the labels with formatted text
                labelSolarVolt.Text = solarCalc.FormatVoltage(solarCalc.AnalogVoltage[0]); // A0 solar panel
                labelBatteryVolt.Text = solarCalc.FormatVoltage(solarCalc.AnalogVoltage[2]); // A2 battery
                labelBatteryCurrent.Text = solarCalc.GetBatteryCurrentText();
                labelLED1Current.Text = solarCalc.GetLed1CurrentText();
                labelLED2Current.Text = solarCalc.GetLed2CurrentText();

                WriteSolarPercentForUnity();
            });
        }

        private void WriteSolarPercentForUnity()
        {
            try
            {
                // get the solar voltage from our calc in volts
                double solarVoltage = solarCalc.GetSolarVoltage();

                // map voltage to 0–100% for the game
                double percent = (solarVoltage / MaxSolarVoltageForGame) * 100.0;

                if (percent < 0) percent = 0;
                if (percent > 100) percent = 100;

                // make sure folder exists (e.g. C:\Temp)
                var dir = Path.GetDirectoryName(SolarPercentFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                // write as plain text (e.g. 37.5)
                File.WriteAllText(
                    SolarPercentFilePath,
                    percent.ToString("F1", CultureInfo.InvariantCulture)
                );

                // optional debug
                Trace.WriteLine($"Solar percent for Unity: {percent:F1}%");
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Error writing solar percent file: " + ex.Message);
            }
        }

        private void SetupFTDIdeviceByAutoSerialNumber()
        {
            // open the first FTDI device we found earlier
            ftStatus = myFtdiDevice.OpenBySerialNumber(ftdiDeviceList[0].SerialNumber);
            if (ftStatus != FTDI.FT_STATUS.FT_OK)
            {
                Trace.WriteLine("Failed to open device (error " + ftStatus.ToString() + ")");
                return;
            }

            // set the baud rate to match our board
            ftStatus = myFtdiDevice.SetBaudRate(115200);
            if (ftStatus != FTDI.FT_STATUS.FT_OK)
            {
                Trace.WriteLine("Failed to set Baud rate (error " + ftStatus.ToString() + ")");
                return;
            }

            // set it to 8 data bits, 1 stop bit, no parity (normal serial)
            ftStatus = myFtdiDevice.SetDataCharacteristics(
                FTDI.FT_DATA_BITS.FT_BITS_8,
                FTDI.FT_STOP_BITS.FT_STOP_BITS_1,
                FTDI.FT_PARITY.FT_PARITY_NONE);
            if (ftStatus != FTDI.FT_STATUS.FT_OK)
            {
                Trace.WriteLine("Failed to set data characteristics (error " + ftStatus.ToString() + ")");
                return;
            }

            // set timeouts so read doesn’t hang forever
            ftStatus = myFtdiDevice.SetTimeouts(5000, 0);
            if (ftStatus != FTDI.FT_STATUS.FT_OK)
            {
                Trace.WriteLine("Failed to set timeouts (error " + ftStatus.ToString() + ")");
                return;
            }

            // here we wait until a full packet is waiting to be read
            UInt32 numBytesAvailable = 0;
            do
            {
                ftStatus = myFtdiDevice.GetRxBytesAvailable(ref numBytesAvailable);
                if (ftStatus != FTDI.FT_STATUS.FT_OK)
                {
                    Trace.WriteLine("Failed to get number of bytes available to read (error " + ftStatus.ToString() + ")");
                    return;
                }
            } while (numBytesAvailable < packetSize);

            // now we actually read it once
            string readData;
            UInt32 numBytesRead = 0;
            ftStatus = myFtdiDevice.Read(out readData, numBytesAvailable, ref numBytesRead);
            if (ftStatus != FTDI.FT_STATUS.FT_OK)
            {
                Trace.WriteLine("Failed to read data (error " + ftStatus.ToString() + ")");
                return;
            }
        }

        private void RefreshDeviceList()
        {
            // ask FTDI how many devices there are
            ftStatus = myFtdiDevice.GetNumberOfDevices(ref ftdiDeviceCount);
            if (ftStatus == FTDI.FT_STATUS.FT_OK)
            {
                Trace.WriteLine("Number of FTDI devices: " + ftdiDeviceCount.ToString());
                Trace.WriteLine("");
            }
            else
            {
                Trace.WriteLine("Failed to get number of devices (error " + ftStatus.ToString() + ")");
                return;
            }

            // if none, then nothing to do
            if (ftdiDeviceCount == 0)
            {
                Trace.WriteLine("Failed to get number of devices (error " + ftStatus.ToString() + ")");
                return;
            }

            // fill our array with info about the devices
            ftStatus = myFtdiDevice.GetDeviceList(ftdiDeviceList);

            if (ftStatus == FTDI.FT_STATUS.FT_OK)
            {
                // print info for each device
                for (UInt32 i = 0; i < ftdiDeviceCount; i++)
                {
                    Trace.WriteLine("Device Index: " + i.ToString());
                    Trace.WriteLine("Flags: " + String.Format("{0:x}", ftdiDeviceList[i].Flags));
                    Trace.WriteLine("Type: " + ftdiDeviceList[i].Type.ToString());
                    Trace.WriteLine("ID: " + String.Format("{0:x}", ftdiDeviceList[i].ID));
                    Trace.WriteLine("Location ID: " + String.Format("{0:x}", ftdiDeviceList[i].LocId));
                    Trace.WriteLine("Serial Number: " + ftdiDeviceList[i].SerialNumber.ToString());
                    Trace.WriteLine("Description: " + ftdiDeviceList[i].Description.ToString());
                    Trace.WriteLine("");
                }
            }

        }

        private void btnClear_Clicked(object sender, EventArgs e)
        {
            // clear the two text areas on the screen
            LabelRXdata.Text = "";
            LabelParsedData.Text = "";
        }

        private void btnSend_Clicked(object sender, EventArgs e)
        {
            try
            {
                // get what user typed
                string messageOut = entrySend.Text;
                messageOut += "\r\n"; // add CRLF
                byte[] messageBytes = Encoding.ASCII.GetBytes(messageOut); // make it bytes

                // send to FTDI
                ftStatus = myFtdiDevice.Write(messageBytes, messageBytes.Length, ref numBytesWritten);
                if (ftStatus != FTDI.FT_STATUS.FT_OK)
                {
                    Trace.WriteLine("Failed to write to device (error " + ftStatus.ToString() + ")");
                    return;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
            }
        }

        private void DeviceIndex_TextChanged(object sender, TextChangedEventArgs e)
        {
            // not used right now
        }

        private void btnBit3_Clicked(object sender, EventArgs e)
        {
            ButtonClicked(3); // tell helper which bit button we pressed
        }

        private void btnBit2_Clicked(object sender, EventArgs e)
        {
            ButtonClicked(2);
        }

        private void btnBit1_Clicked(object sender, EventArgs e)
        {
            ButtonClicked(1);
        }

        private void btnBit0_Clicked(object sender, EventArgs e)
        {
            ButtonClicked(0);
        }

        private void ButtonClicked(int i)
        {
            // make an array of the 4 bit buttons so we can pick by index
            Button[] btnBit = new Button[] { btnBit0, btnBit1, btnBit2, btnBit3 };

            // if that button currently shows 0, change to 1
            if (btnBit[i].Text == "0")
            {
                btnBit[i].Text = "1";              // update button text
                stringBuilderSend[i + 3] = '1';    // update the packet string (bits start at index 3)
            }
            else // if it was 1 then make it 0
            {
                btnBit[i].Text = "0";
                stringBuilderSend[i + 3] = '0';
            }
            // after changing the bits, actually send the packet
            SendPacket();
        }

        private void SendPacket()
        {
            int calSendChkSum = 0; // we will calculate our checksum here
            try
            {
                string messageOut = stringBuilderSend.ToString(); // get current packet text

                // checksum is based on chars 3..6
                for (int i = 3; i < 7; i++)
                {
                    calSendChkSum += (byte)stringBuilderSend[i];
                }
                calSendChkSum %= 1000; // keep last 3 digits

                // replace old checksum in the string with the new one
                stringBuilderSend.Remove(7, 3); // remove 3 chars where checksum goes
                stringBuilderSend.Insert(7, calSendChkSum.ToString()); // put the new checksum in

                // show it in the entry box too
                entrySend.Text = stringBuilderSend.ToString();
                messageOut += "\r\n"; // add CRLF so the other side knows it's done
                byte[] messageBytes = Encoding.ASCII.GetBytes(messageOut); // turn into bytes

                // send to FTDI
                ftStatus = myFtdiDevice.Write(messageBytes, messageBytes.Length, ref numBytesWritten);
                if (ftStatus != FTDI.FT_STATUS.FT_OK)
                {
                    Trace.WriteLine("Failed to write to device (error " + ftStatus.ToString() + ")");
                    return;
                }
            }
            catch (Exception ex)
            {
                // if something goes wrong, print it
                Trace.WriteLine(ex.Message);
            }
        }
    }
}
