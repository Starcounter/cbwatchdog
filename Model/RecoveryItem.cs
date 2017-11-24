using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CustomWatchdog
{
    internal class RecoveryItem
    {
        public string RecoveryBatch { get; set; }
        public uint OverrideRecoveryExecutionTimeout { get; set; }
        public string StarcounterBinDirectory { get; set; }
        public string ScDatabase { get; set; }
        public List<string> Processes { get; set; } = new List<string>();
        public List<string> ScAppNames { get; set; } = new List<string>();

        public override string ToString()
        {
            string info =
            "    {" + "\n" + 
            "        recoveryBatch : " + RecoveryBatch.ToString() + "\n" +
            "        scDatabase : " + ScDatabase.ToString() + "\n" +
            "        processes : " + string.Join(";", Processes) + "\n" +
            "        scAppNames : " + string.Join(";", ScAppNames) + "\n" +
            "    }" + "\n";
            
            return info;
        }
    }
}
