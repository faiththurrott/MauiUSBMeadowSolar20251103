using System;

namespace MauiUSBMeadowSolar20251103
{
    public class SolarCalc
    {
        // resistor values from the lab circuit
        const float R_BATT = 100f;   // A1 to A2
        const float R_LED1 = 220f;   // A1 to A4
        const float R_LED2 = 220f;   // A1 to A3

        // what MainPage expects:
        // solarCalc.AnalogVoltage[0], etc.
        public int[] AnalogVoltage = new int[6];

        public int Binary { get; private set; }

        public void ParseSolarData(string packet)
        {
            // make sure we don't crash on short packets
            if (string.IsNullOrWhiteSpace(packet)) return;
            if (packet.Length < 34) return;

            // packet format: ###NNN A0 A1 A2 A3 A4 A5 BIN CHK
            AnalogVoltage[0] = Read4(packet, 6);  // A0
            AnalogVoltage[1] = Read4(packet, 10);  // A1
            AnalogVoltage[2] = Read4(packet, 14);  // A2
            AnalogVoltage[3] = Read4(packet, 18);  // A3
            AnalogVoltage[4] = Read4(packet, 22);  // A4
            AnalogVoltage[5] = Read4(packet, 26);  // A5
            Binary = Read4(packet, 30);  // binary word
        }

        private int Read4(string text, int pos)
        {
            if (text.Length < pos + 4)
                return 0;

            string part = text.Substring(pos, 4);
            if (int.TryParse(part, out int value))
                return value;
            return 0;
        }

        public string FormatVoltage(int mV)
        {
            float v = mV / 1000f;
            return v.ToString("0.000") + " V";
        }

        public string GetBatteryCurrentText()
        {
            float vA1 = AnalogVoltage[1] / 1000f;
            float vA2 = AnalogVoltage[2] / 1000f;
            float iA = (vA1 - vA2) / R_BATT;
            return (iA * 1000f).ToString("0.00") + " mA";
        }

        public string GetLed1CurrentText()
        {
            float vA1 = AnalogVoltage[1] / 1000f;
            float vA4 = AnalogVoltage[4] / 1000f;
            float iA = (vA1 - vA4) / R_LED1;
            return (iA * 1000f).ToString("0.00") + " mA";
        }

        public string GetLed2CurrentText()
        {
            float vA1 = AnalogVoltage[1] / 1000f;
            float vA3 = AnalogVoltage[3] / 1000f;
            float iA = (vA1 - vA3) / R_LED2;
            return (iA * 1000f).ToString("0.00") + " mA";
        }
    }
}
