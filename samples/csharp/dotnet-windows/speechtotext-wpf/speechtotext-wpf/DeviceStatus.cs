using System;
using System.Collections.Generic;
using System.Linq;
using System.Speech.Synthesis;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Markup;
using Microsoft.Recognizers.Text;
using Microsoft.Recognizers.Text.Number;

namespace speechtotextwpf
{
    public class DeviceStatus
    {
        public bool IsBotAwake = false;
        public bool IsLightOn = false;
        public bool IsProjectorOn = false;
        public int CoolerTemperature = 24;
        public int TempDegree = 1;

        public static int ParserNumberFromText(string text, int defValue = 0)
        {
            var number = defValue;

            const string DefaultCulture = Culture.Chinese;
            var result = NumberRecognizer.RecognizeNumber(text, DefaultCulture);
            if (result.Count > 0)
            {
                var resolution = result[0].Resolution;
                if (resolution.ContainsKey("value"))
                {
                    int.TryParse(resolution["value"].ToString(), out number);
                }
            }

            return number;
        }
    }
}
