using System;

namespace MauiUSBMeadowSolar20251103 
{
    public class SolarCalc // this is the class we made to do the solar calcs
    {
        // these are the resistor values 
        const double R_BATT = 100d;   // this one is the 100Ω between A1 and A2
        const double R_LED1 = 220d;   // this is 220Ω for LED1 (A1 to A4)
        const double R_LED2 = 220d;   // this is 220Ω for LED2 (A1 to A3)

        // this is where we store the 6 analog readings (A0..A5) in mV
        public int[] AnalogVoltage = new int[6]; // make an array with 6 spots

        public int Binary { get; private set; } // this will hold the binary/status number from the packet

        public void ParseSolarData(string packet) // this reads the packet that comes in as text
        {
            // if packet is empty or null, just stop so we don't crash
            if (string.IsNullOrWhiteSpace(packet)) return;
            // if packet is too short and doesn't have all the numbers, stop
            if (packet.Length < 34) return;

            // packet looks like: ###NNN A0 A1 A2 A3 A4 A5 BIN CHK
            // we read 4 characters at the right spot for each value
            AnalogVoltage[0] = Read4(packet, 6);  // A0 lives starting at spot 6
            AnalogVoltage[1] = Read4(packet, 10);  // A1 starts at 10
            AnalogVoltage[2] = Read4(packet, 14);  // A2 starts at 14
            AnalogVoltage[3] = Read4(packet, 18);  // A3 starts at 18
            AnalogVoltage[4] = Read4(packet, 22);  // A4 starts at 22
            AnalogVoltage[5] = Read4(packet, 26);  // A5 starts at 26
            Binary = Read4(packet, 30);  // the binary word starts at 30
        }

        private int Read4(string text, int pos) // this helper reads 4 characters and turns them into a number
        {
            // if there aren’t 4 characters there, just return 0
            if (text.Length < pos + 4)
                return 0;

            string part = text.Substring(pos, 4); // grab 4 characters from the text
            if (int.TryParse(part, out int value)) // try to turn those 4 characters into an int
                return value; // if it worked, give back the number
            return 0; // if it didn’t work, just give back 0
        }

        public string FormatVoltage(int mV) // this makes "x.xxx V" from millivolts
        {
            double v = mV / 1000d; // change mV to volts
            return v.ToString("0.000") + " V"; // make it look nice with 3 decimals
        }

        public string GetBatteryCurrentText() // this figures out the battery current
        {
            double vA1 = AnalogVoltage[1] / 1000d; // make A1 into volts
            double vA2 = AnalogVoltage[2] / 1000d; // make A2 into volts
            double iA = (vA1 - vA2) / R_BATT; // current = voltage drop / resistor
            return (iA * 100d).ToString("0.00") + " mA"; // change to mA and make it a string
        }

        public string GetLed1CurrentText() // this figures out LED1 current
        {
            double vA1 = AnalogVoltage[1] / 1000d; // A1 in volts
            double vA4 = AnalogVoltage[4] / 1000d; // A4 in volts
            double iA = (vA1 - vA4) / R_LED1; // current = voltage drop / 220Ω
            return (iA * 100d).ToString("0.00") + " mA"; // show it in mA
        }

        public string GetLed2CurrentText() // this figures out LED2 current
        {
            double vA1 = AnalogVoltage[1] / 1000d; // A1 in volts
            double vA3 = AnalogVoltage[3] / 1000d; // A3 in volts
            double iA = (vA1 - vA3) / R_LED2; // current = voltage drop / 220Ω
            return (iA * 1000d).ToString("0.00") + " mA"; // show it in mA
        }
         public double GetSolarVoltage()
        {
            return AnalogVoltage[0] / 1000d;  // A0 is solar panel in mV
        }
        public double SolarVoltageVolts => AnalogVoltage[0] / 1000d;  // A0
        public double BatteryVoltageVolts => AnalogVoltage[2] / 1000d; // A2
    }
}
