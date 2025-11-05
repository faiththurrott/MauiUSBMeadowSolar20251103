using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;


namespace MauiUSBMeadowSolar20251103
{
    public class SolarCalc
    {
        private const float R1_BATT = 100f;
        private const float R2_LED1 = 220f;
        private const float R3_LED2 = 220f;

        public int[] AnalogVoltage { get; private set; } = new int[6];

        public int BinaryData { get; private set; }

        public void ParseSolarData(string packet)
        {
            if (string.IsNullOrWhiteSpace(packet)) return;
            if (packet.Length < 34) return;

            AnalogVoltage[0] = Parse4(packet.Substring(6, 4));   // A0 solar
            AnalogVoltage[1] = Parse4(packet.Substring(10, 4));  // A1
            AnalogVoltage[2] = Parse4(packet.Substring(14, 4));  // A2 cap
            AnalogVoltage[3] = Parse4(packet.Substring(18, 4));  // A3 LED2
            AnalogVoltage[4] = Parse4(packet.Substring(22, 4));  // A4 LED1
            AnalogVoltage[5] = Parse4(packet.Substring(26, 4));  // extra
            BinaryData = Parse4(packet.Substring(30, 4));
        }

        private int Parse4(string s)
        {
            if (int.TryParse(s, out int v))
                return v;
            return 0;
        }

        public string FormatVoltage(int mV)
        {
            float v = mV / 1000f;
            return v.ToString("0.000", CultureInfo.InvariantCulture) + " V";
        }

        public string GetBatteryCurrentText()
        {
            float vA1 = AnalogVoltage[1] / 1000f;
            float vA2 = AnalogVoltage[2] / 1000f;
            float iA = (vA1 - vA2) / R1_BATT;
            float i_mA = iA * 1000f;
            return i_mA.ToString("0.0", CultureInfo.InvariantCulture) + " mA";
        }

        public string GetLed1CurrentText()
        {
            float vA1 = AnalogVoltage[1] / 1000f;
            float vA4 = AnalogVoltage[4] / 1000f;
            float iA = (vA1 - vA4) / R2_LED1;
            float i_mA = iA * 1000f;
            return i_mA.ToString("0.0", CultureInfo.InvariantCulture) + " mA";
        }

        public string GetLed2CurrentText()
        {
            float vA1 = AnalogVoltage[1] / 1000f;
            float vA3 = AnalogVoltage[3] / 1000f;
            float iA = (vA1 - vA3) / R3_LED2;
            float i_mA = iA * 1000f;
            return i_mA.ToString("0.0", CultureInfo.InvariantCulture) + " mA";
        }
    }
}
