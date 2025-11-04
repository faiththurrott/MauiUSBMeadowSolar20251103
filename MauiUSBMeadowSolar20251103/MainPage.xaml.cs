using FTD2XX_NET;
using System.Diagnostics;
using System.Text;


namespace MauiUSBMeadowSolar20251103 // updated name
{
    public partial class MainPage : ContentPage // container for code file
    {
        UInt32 ftdiDeviceCount = 0; // how many FTDI devices were found
        FTDI.FT_STATUS ftStatus = FTDI.FT_STATUS.FT_OK; // Last FTDO API call result


        FTDI myFtdiDevice = new FTDI(); // FTDI device we use to talk over USB


        FTDI.FT_DEVICE_INFO_NODE[] ftdiDeviceList = new FTDI.FT_DEVICE_INFO_NODE[32];


        private uint packetSize = 37; // Expected packet length from MEADOW

        private uint numBytesWritten; // How many bytes were written on last send
        private string newPacket; // Most recently captured full packet as text
        private bool sendDataOK; // track if last send looked OK
        private bool bPortOpen; // Are we currently connected to a device?
        private bool readData;
        private int newPacketNumber; // Parsed packet number from the incoming packet
        private int lastPkt; // Last packet number seen (for loss/rollover detection)
        private int totalLost; // Count of missing/lost packets
        private int totalRollovers; // Count of 999->000 rollovers detected
        private int totalChkErrors; // Count of packets with checksum errors

        public MainPage()
        {
            InitializeComponent();
        }

        private void btnOpenClose_Clicked(object sender, EventArgs e)
        {
            if (!bPortOpen) // If not open yet, try to open
            {
                RefreshDeviceList(); // Ask FTDI driver for connected devices
                sendDataOK = false; // Reset send status flag
                SetupFTDdeviceByAutoSerialNumber(); // Open first device and configure UART
                _ = MonitorUSBData(); // Start the async read loop (fire-and-forget)
                btnOpenClose.Text = "Close"; // Update button label
                bPortOpen = true; // Remember that we are now open
            }
            else // Already open → close it
            {
                CloseFTDIDevice(); // Close the FTDI port
                btnOpenClose.Text = "Open"; // Update button label back
                bPortOpen = false; // Remember we are closed
            }
        }

        private void CloseFTDIDevice()
        {
            Console.WriteLine(readData); // (Debug) print read flag
            ftStatus = myFtdiDevice.Close(); // Tell FTDI driver to close the handle
            return; // Done
        }

        private async Task MonitorUSBData()
        {

            while (myFtdiDevice.IsOpen) // Keep looping while the device is open
            {
                UInt32 numBytesAvailable = 0; // Will store how many bytes are waiting
                do // Poll until at least a full packet is buffered
                {
                    ftStatus = myFtdiDevice.GetRxBytesAvailable(ref numBytesAvailable); // Ask FTDI how many bytes can be read
                    if (ftStatus != FTDI.FT_STATUS.FT_OK) // If the driver reported an error
                    {
                        Trace.WriteLine("Failed to get number of bytes available to read (error " + ftStatus.ToString() + ")"); // Log it
                        return; // Bail out of the loop/task
                    }
                    Thread.Sleep(10); // Small pause to avoid tight CPU loop and let more bytes arrive
                } while (numBytesAvailable < packetSize); // Continue until we have at least one whole packet


                string readDataStr; // Will hold the text we read
                UInt32 numBytesRead = 0; // Will hold how many bytes we actually read
                ftStatus = myFtdiDevice.Read(out readDataStr, numBytesAvailable, ref numBytesRead); // Read available bytes as a string
                if (ftStatus != FTDI.FT_STATUS.FT_OK) // If the read failed
                {
                    Trace.WriteLine("Failed to read data (error " + ftStatus.ToString() + ")"); // Log it
                    return; // Stop monitoring on error
                }


                var s = readDataStr; // Work on a local copy of the string
                if (s.EndsWith("\r\n")) // If the string ends with CRLF 
                    s = s.Substring(0, s.Length - 2);
                if (s.Length >= packetSize) // If we have at least a full packet
                    newPacket = s.Substring(0, (int)packetSize); // Keep exactly one packet worth of data
                else // Not enough data for a packet 
                    continue;


                MainThread.BeginInvokeOnMainThread(() => // Update UI on the main thread
                {
                    if (checkBoxParsedHistory.IsChecked == true) // If user wants to keep history
                        LabelRXdata.Text = newPacket + LabelRXdata.Text; // Prepend new packet to existing text
                    else // Show only latest packet
                        LabelRXdata.Text = newPacket; // Replace with latest
                });


                await Task.Delay(50); // Small delay to avoid UI spam and give time to parse


                if (newPacket.Length >= packetSize) // Double check length safety before parsing
                {
                    parseNewPacket(newPacket); // Send the packet to our parser
                }
            }
        }


        private void parseNewPacket(string pkt)
        {
            if (pkt.Length < 37 || pkt.Substring(0, 3) != "###") // Basic guard: must be 37+ chars and start with header
                return;


            int calChkSum = 0; // Will hold the computed checksum
            newPacketNumber = Convert.ToInt32(pkt.Substring(3, 3)); // Parse packet number 


            for (int i = 3; i < 34; i++) // Sum bytes 3..33 inclusive 
                calChkSum += (byte)pkt[i]; // Add ASCII byte values to checksum
            calChkSum %= 1000; // Keep only last 3 digits (0..999)


            int recChkSum = Convert.ToInt32(pkt.Substring(34, 3)); // Parse the 3-digit checksum that came in the packet


            if (calChkSum == recChkSum) // Good packet if checksums match
            {
                Trace.WriteLine("Packet number: " + newPacketNumber); // Debug: show packet number
                Trace.WriteLine("Checksum: " + recChkSum); // Debug: show checksum
                Trace.WriteLine("Data: " + pkt.Substring(3, 31)); // Debug: show the data region



                string parsedData =
                $"{pkt.Length,-14}" + // Column: total length of the packet text
                $"{pkt.Substring(0, 3),-14}" + // Column: header "###"
                $"{pkt.Substring(3, 3),-14}" + // Column: packet number (3 digits)
                $"{pkt.Substring(6, 4),-14}" + // Column: payload segment 1 (4 chars)
                $"{pkt.Substring(10, 4),-14}" + // Column: payload segment 2 (4 chars)
                $"{pkt.Substring(14, 4),-14}" + // Column: payload segment 3 (4 chars)
                $"{pkt.Substring(18, 4),-14}" + // Column: payload segment 4 (4 chars)
                $"{pkt.Substring(22, 4),-14}" + // Column: payload segment 5 (4 chars)
                $"{pkt.Substring(26, 4),-14}" + // Column: payload segment 6 (4 chars)
                $"{pkt.Substring(30, 4),-14}" + // Column: payload segment 7 (4 chars)
                $"{pkt.Substring(34, 3),-14}" + // Column: received checksum (3 chars)
                $"{calChkSum,-14}\r\n"; // Column: computed checksum (as number)


                MainThread.BeginInvokeOnMainThread(() => // Update UI on the main thread
                {
                    if (checkBoxParsedHistory.IsChecked == true) // Keep history if checked
                        LabelParsedData.Text = parsedData + LabelParsedData.Text; // Prepend new parsed line
                    else // Show only latest parsed line
                        LabelParsedData.Text = parsedData; // Replace content
                });
            }
            else
            {
                totalChkErrors++; // Increment error counter
                Trace.WriteLine("Checksum error"); // Log the error
            }
        }

        private void SetupFTDdeviceByAutoSerialNumber()
        {
            ftStatus = myFtdiDevice.OpenBySerialNumber(ftdiDeviceList[0].SerialNumber); // Open the first discovered device
            if (ftStatus != FTDI.FT_STATUS.FT_OK) // If open failed
            {
                Trace.WriteLine("Failed to open device (error " + ftStatus.ToString() + ")"); // Log error
                return; // Stop here
            }


            ftStatus = myFtdiDevice.SetBaudRate(115200); // Set baud rate to 115200
            if (ftStatus != FTDI.FT_STATUS.FT_OK) // If it failed
            {
                Trace.WriteLine("Failed to set Baud rate (error " + ftStatus.ToString() + ")"); // Log error
                return; // Stop here
            }


            ftStatus = myFtdiDevice.SetDataCharacteristics(
            FTDI.FT_DATA_BITS.FT_BITS_8,
            FTDI.FT_STOP_BITS.FT_STOP_BITS_1,
            FTDI.FT_PARITY.FT_PARITY_NONE);
            if (ftStatus != FTDI.FT_STATUS.FT_OK) // If it failed
            {
                Trace.WriteLine("Failed to set data characteristics (error " + ftStatus.ToString() + ")"); // Log error
                return; // Stop here
            }


            myFtdiDevice.SetFlowControl(FTDI.FT_FLOW_CONTROL.FT_FLOW_NONE, 0x00, 0x00); // Disable hardware flow control (RTS/CTS)


            ftStatus = myFtdiDevice.SetTimeouts(5000, 0); // Set read timeout 5000ms, write timeout 0ms
            if (ftStatus != FTDI.FT_STATUS.FT_OK) // If it failed
            {
                Trace.WriteLine("Failed to set timeouts (error " + ftStatus.ToString() + ")"); // Log error
                return;
            }

        }

        private void RefreshDeviceList()
        {
            ftStatus = myFtdiDevice.GetNumberOfDevices(ref ftdiDeviceCount); // Ask driver for device count
            if (ftStatus != FTDI.FT_STATUS.FT_OK) // If call failed
            {
                Trace.WriteLine("Failed to get number of devices (error " + ftStatus.ToString() + ")"); // Log error
                return; // Stop here
            }


            if (ftdiDeviceCount == 0) // If no devices are connected
            {
                Trace.WriteLine("Failed to get number of devices (error " + ftStatus.ToString() + ")");
                return; // Stop here
            }


            ftStatus = myFtdiDevice.GetDeviceList(ftdiDeviceList); // Fill the array with info for each device


            if (ftStatus == FTDI.FT_STATUS.FT_OK) // If successful
            {
                for (UInt32 i = 0; i < ftdiDeviceCount; i++) // Loop over each device
                {
                    Trace.WriteLine("Device Index: " + i.ToString()); // Show its index
                    Trace.WriteLine("Flags: " + String.Format("{0:x}", ftdiDeviceList[i].Flags)); // Show flags
                    Trace.WriteLine("Type: " + ftdiDeviceList[i].Type.ToString()); // Show device type
                    Trace.WriteLine("ID: " + String.Format("{0:x}", ftdiDeviceList[i].ID)); // Show USB ID
                    Trace.WriteLine("Location ID: " + String.Format("{0:x}", ftdiDeviceList[i].LocId)); // Show hub/port location id
                    Trace.WriteLine("Serial Number: " + ftdiDeviceList[i].SerialNumber.ToString()); // Show serial number
                    Trace.WriteLine("Description: " + ftdiDeviceList[i].Description.ToString()); // Show description string
                    Trace.WriteLine(""); // Blank line for readability
                }
            }
        }

        private void btnClear_Clicked(object sender, EventArgs e)
        {
            // Clear the UI
            LabelRXdata.Text = string.Empty;
            LabelParsedData.Text = string.Empty;
            lblMessage.Text = string.Empty;



        }


        private void btnSend_Clicked(object sender, EventArgs e)
        {
            try // Catch any exceptions from building/sending the message
            {
                string text = (entrySend.Text ?? "").Trim(); // Grab text from input box 
                string messageOut; // Will hold the final string we send out



                string bits = null; // Will store just the 4 control bits if we find them
                if (text.StartsWith("###") && text.Length >= 7) // Case 1: user typed header already
                {
                    var cand = text.Substring(3, 4); // Extract 4 chars after ###
                    if (cand.All(c => c == '0' || c == '1')) bits = cand; // Accept only if they are 0/1
                }
                else if (text.Length == 4 && text.All(c => c == '0' || c == '1')) // Case 2: user typed exactly 4 bits
                {
                    bits = text; // Use them directly
                }
                else if (text.Length >= 7 && text.Substring(0, 4).All(c => c == '0' || c == '1')) // Case 3: first 4 chars are bits
                {
                    bits = text.Substring(0, 4); // Use those
                }


                if (bits != null) // If we found 4 valid bits
                {
                    int chk = (byte)bits[0] + (byte)bits[1] + (byte)bits[2] + (byte)bits[3]; // Simple checksum: sum ASCII codes of 4 bits
                    chk %= 1000; // Keep to 3 digits
                    messageOut = "###" + bits + chk.ToString("D3") + "\r\n"; // Build full line with header and CRLF
                }
                else
                {
                    messageOut = text + "\r\n"; // Add CRLF line ending
                }


                byte[] messageBytes = Encoding.ASCII.GetBytes(messageOut); // Convert the string to raw bytes
                ftStatus = myFtdiDevice.Write(messageBytes, messageBytes.Length, ref numBytesWritten); // Write bytes to the FTDI device
                if (ftStatus != FTDI.FT_STATUS.FT_OK) // If write failed
                {
                    Trace.WriteLine("Failed to write to device (error " + ftStatus.ToString() + ")"); // Log error
                    return;
                }

            }
            catch (Exception ex) // If something threw an exception
            {
                Trace.WriteLine(ex.Message); // Log the exception message
            }
        }
    }
}

