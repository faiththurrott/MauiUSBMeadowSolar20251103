using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Threading;

using FTD2XX_NET;

namespace MauiUSBMeadowSolar20251103
{
    public partial class MainPage : ContentPage
    {
        //https://ftdichip.com/software-examples/code-examples/csharp-examples

        UInt32 ftdiDeviceCount = 0;
        FTDI.FT_STATUS ftStatus = FTDI.FT_STATUS.FT_OK;

        // Create new instance of the FTDI device class
        FTDI myFtdiDevice = new FTDI();

        // Allocate storage for device info list
        FTDI.FT_DEVICE_INFO_NODE[] ftdiDeviceList = new FTDI.FT_DEVICE_INFO_NODE[64];
        private uint packetSize = 39;
        private uint numBytesWritten;
        private bool bPortOpen = false;
        private bool sendDataOk;
        private string newPacket;
        //private int newPacketNumber;
        private int oldPackNumber = -1;
        private int newPacketNumber = 0;
        private int lostPacketCount = 0;
        private int packetRollover = 0;
        private int chkSumError = 0;

        //SerialPort serialPort = new SerialPort();
        StringBuilder stringBuilderSend = new StringBuilder("###1111196");

        SolarCalc solarCalc = new SolarCalc();

        public MainPage()
        {
            InitializeComponent();
        }

        private void btnOpenClose_Clicked(object sender, EventArgs e)
        {
            if (!bPortOpen)
            {
                RefreshDeviceList(); // Call method to update the list
                sendDataOk = false; // set the flag to false
                SetupFTDIdeviceByAutoSerialNumber();
                _ = MonitorUSBdata();
                btnOpenClose.Text = "Close";
                bPortOpen = true;
            }
            else
            {
                CloseFtdiDevice();
                btnOpenClose.Text = "Open";
                bPortOpen = false;
            }
        }

        private void CloseFtdiDevice()
        {
            ftStatus = myFtdiDevice.Close();
            return;
        }

        private async Task MonitorUSBdata()
        {
            while (myFtdiDevice.IsOpen)
            {
                // Check the amount of data available to read
                // In this case we know how much data we are expecting, 
                // so wait until we have all of the bytes we have sent.
                UInt32 numBytesAvailable = 0;
                do
                {
                    ftStatus = myFtdiDevice.GetRxBytesAvailable(ref numBytesAvailable);
                    if (ftStatus != FTDI.FT_STATUS.FT_OK)
                    {
                        // Wait for a key press
                        Trace.WriteLine("Failed to get number of bytes available to read (error " + ftStatus.ToString() + ")");
                        return;
                    }
                } while (numBytesAvailable < packetSize);

                // Now that we have the amount of data we want available, read it
                string readData;
                UInt32 numBytesRead = 0;
                //Note that the read method is overload, so can read string or byte array data
                ftStatus = myFtdiDevice.Read(out readData, numBytesAvailable, ref numBytesRead);
                if (ftStatus != FTDI.FT_STATUS.FT_OK)
                {
                    // Wait for a key press
                    Trace.WriteLine("Failed to read data (error " + ftStatus.ToString() + ")");
                    return;
                }
                Trace.WriteLine("Read data: " + readData);
                newPacket = readData;
                if (checkBoxHistory.IsChecked)
                {
                    LabelRXdata.Text = newPacket + LabelRXdata.Text;
                }
                else
                {
                    LabelRXdata.Text = newPacket;
                }
                await Task.Delay(90);
                if (newPacket.Length >= packetSize)
                {
                    ParseNewPacket();
                }

            }
        }

        private void ParseNewPacket()
        {
            if (newPacket.Substring(0, 3) == "###")
            {
                int calChkSum = 0;
                newPacketNumber = Convert.ToInt32(newPacket.Substring(3, 3));
                for (int i = 3; i < 34; i++)
                {
                    calChkSum += (byte)newPacket[i];
                }
                calChkSum %= 1000;
                int recChkSum = Convert.ToInt32(newPacket.Substring(34, 3));
                if (calChkSum == recChkSum)
                {

                    newPacketNumber = Convert.ToInt32(newPacket.Substring(3, 3));


                    if (oldPackNumber > -1)
                    {
                        if (newPacketNumber < oldPackNumber)
                        {
                            packetRollover++;
                            if (oldPackNumber != 999)
                            {
                                lostPacketCount += 999 - oldPackNumber + newPacketNumber;
                            }
                        }
                        else
                        {
                            if (newPacketNumber != oldPackNumber + 1)
                            {
                                lostPacketCount += newPacketNumber - oldPackNumber - 1;
                            }
                        }
                    }
                    for (int i = 3; i < 34; i++)
                    {
                        calChkSum += (byte)newPacket[i];
                    }
                    calChkSum %= 1000;
                    recChkSum = Convert.ToInt32(newPacket.Substring(34, 3));
                    if (recChkSum == calChkSum)
                    {
                        DisplaySolarData(newPacket);
                        oldPackNumber = newPacketNumber;
                    }
                    else
                    {
                        chkSumError++;
                    }

                    Trace.WriteLine("Packet number: " + newPacketNumber);
                    Trace.WriteLine("Checksum: " + recChkSum);
                    Trace.WriteLine("Data: " + newPacket.Substring(3, 31));
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
                        $"{newPacket.Substring(34, 3),-14}" +   // FIX: 3 chars, not 4
                        $"{calChkSum,-14}" +
                        $"{lostPacketCount,-14}" +
                        $"{chkSumError,-14}" +
                        $"{packetRollover}\r\n";

                    if (checkBoxParsedHistory.IsChecked)
                    {
                        LabelParsedData.Text = parsedData + LabelParsedData.Text;
                    }
                    else
                    {
                        LabelParsedData.Text = parsedData;
                    }
                }
                else
                {
                    Trace.WriteLine("Checksum error");
                }
            }
        }

        private void DisplaySolarData(string validPacket)
        {
            //SolarCalc.ParseSolarData(validPacket);
            //labelSolarVolt.Text = solarCalc.GetVoltage(SolarCalc.analogVoltage[0]);
            //labelBatteryVolt.Text = solarCalc.GetVoltage(SolarCalc.analogVoltage[2]);
            //labelBatteryCurrent.Text = SolarCalc.GetCurrent(solarCalc.analogVoltage[1], solarCalc.analogVoltage[2]);
            //labelLED1Current.Text = solarCalc.GetLedCurrent(solarCalc.analogVoltage[1], solarCalc.AnalogVoltage[4]);
            //labelLED2Current.Text = solarCalc.GetLedCurrent(solarCalc.analogVoltage[1], solarCalc.AnalogVoltage[3]);

        }

        private void SetupFTDIdeviceByAutoSerialNumber()
        {
            // Open first device in our list by serial number
            ftStatus = myFtdiDevice.OpenBySerialNumber(ftdiDeviceList[0].SerialNumber);
            if (ftStatus != FTDI.FT_STATUS.FT_OK)
            {
                // Wait for a key press
                Trace.WriteLine("Failed to open device (error " + ftStatus.ToString() + ")");
                return;
            }

            // Set up device data parameters
            // Set Baud rate
            ftStatus = myFtdiDevice.SetBaudRate(115200);
            if (ftStatus != FTDI.FT_STATUS.FT_OK)
            {
                // Wait for a key press
                Trace.WriteLine("Failed to set Baud rate (error " + ftStatus.ToString() + ")");
                return;
            }

            // Set data characteristics - Data bits, Stop bits, Parity
            ftStatus = myFtdiDevice.SetDataCharacteristics(FTDI.FT_DATA_BITS.FT_BITS_8, FTDI.FT_STOP_BITS.FT_STOP_BITS_1, FTDI.FT_PARITY.FT_PARITY_NONE);
            if (ftStatus != FTDI.FT_STATUS.FT_OK)
            {
                // Wait for a key press
                Trace.WriteLine("Failed to set data characteristics (error " + ftStatus.ToString() + ")");
                return;
            }

            // Set read timeout to 5 seconds, write timeout to infinite
            ftStatus = myFtdiDevice.SetTimeouts(5000, 0);
            if (ftStatus != FTDI.FT_STATUS.FT_OK)
            {
                // Wait for a key press
                Trace.WriteLine("Failed to set timeouts (error " + ftStatus.ToString() + ")");

                return;
            }

            // Check the amount of data available to read
            // In this case we know how much data we are expecting, 
            // so wait until we have all of the bytes we have sent.
            UInt32 numBytesAvailable = 0;
            do
            {
                ftStatus = myFtdiDevice.GetRxBytesAvailable(ref numBytesAvailable);
                if (ftStatus != FTDI.FT_STATUS.FT_OK)
                {
                    // Wait for a key press
                    Trace.WriteLine("Failed to get number of bytes available to read (error " + ftStatus.ToString() + ")");
                    return;
                }
            } while (numBytesAvailable < packetSize);

            // Now that we have the amount of data we want available, read it
            string readData;
            UInt32 numBytesRead = 0;
            // Note that the Read method is overloaded, so can read string or byte array data
            ftStatus = myFtdiDevice.Read(out readData, numBytesAvailable, ref numBytesRead);
            if (ftStatus != FTDI.FT_STATUS.FT_OK)
            {
                // Wait for a key press
                Trace.WriteLine("Failed to read data (error " + ftStatus.ToString() + ")");
                return;
            }
        }

        private void RefreshDeviceList()
        {
            // Determine the number of FTDI devices connected to the machine
            ftStatus = myFtdiDevice.GetNumberOfDevices(ref ftdiDeviceCount);
            // Check status
            if (ftStatus == FTDI.FT_STATUS.FT_OK)
            {
                Trace.WriteLine("Number of FTDI devices: " + ftdiDeviceCount.ToString());
                Trace.WriteLine("");
            }
            else
            {
                // Wait for a key press
                Trace.WriteLine("Failed to get number of devices (error " + ftStatus.ToString() + ")");
                return;
            }

            // If no devices available, return
            if (ftdiDeviceCount == 0)
            {
                // Wait for a key press
                Trace.WriteLine("Failed to get number of devices (error " + ftStatus.ToString() + ")");
                return;
            }

            // Populate our device list
            ftStatus = myFtdiDevice.GetDeviceList(ftdiDeviceList);

            if (ftStatus == FTDI.FT_STATUS.FT_OK)
            {
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
            LabelRXdata.Text = "";
            LabelParsedData.Text = "";
        }

        private void btnSend_Clicked(object sender, EventArgs e)
        {
            try
            {
                string messageOut = entrySend.Text;
                messageOut += "\r\n";
                byte[] messageBytes = Encoding.ASCII.GetBytes(messageOut);

                // Note that the Write method is overloaded, so can write string or byte array data
                ftStatus = myFtdiDevice.Write(messageBytes, messageBytes.Length, ref numBytesWritten);
                if (ftStatus != FTDI.FT_STATUS.FT_OK)
                {
                    // Wait for a key press
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
            // not implemented.
        }

        private void btnBit3_Clicked(object sender, EventArgs e)
        {
            ButtonClicked(3);
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
            Button[] btnBit = new Button[] { btnBit0, btnBit1, btnBit2, btnBit3 };
            if (btnBit[i].Text== "0")
            {
                btnBit[i].Text = "1";
                stringBuilderSend[i + 3] = '1';
            }
            else
            {
                btnBit[i].Text = "0";
                stringBuilderSend[i + 3] = '0';
            }
            SendPacket();
        }

        private void SendPacket()
        {
            int calSendChkSum = 0;
            try
            {
                string messageOut = stringBuilderSend.ToString();
                for (int i = 3; i <7; i++)
                {
                    calSendChkSum += (byte)stringBuilderSend[i];
                }
                calSendChkSum %= 1000;
                stringBuilderSend.Remove(7, 3);
                stringBuilderSend.Insert(7, calSendChkSum.ToString());
                entrySend.Text = stringBuilderSend.ToString(); ;
                messageOut += "\r\n";
                byte[] messageBytes = Encoding.ASCII.GetBytes(messageOut);

                // Note that the Write method is overloaded, so can write string or byte array data
                ftStatus = myFtdiDevice.Write(messageBytes, messageBytes.Length, ref numBytesWritten);
                if (ftStatus != FTDI.FT_STATUS.FT_OK)
                {
                    // Wait for a key press
                    Trace.WriteLine("Failed to write to device (error " + ftStatus.ToString() + ")");
                    return;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
            }
        }
    }
}
