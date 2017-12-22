using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;

namespace CustomWatchdog
{
    [DataContract]
    public class RecoveryConfigItem : JsonData
    {
        /// <summary>
        /// Each recovery job should use a custom batch file, this is the unique id of the items
        /// </summary>
        [DataMember(Name = "recoveryBatch", IsRequired = true, EmitDefaultValue = false)]
        public string RecoveryBatch { get; set; }

        [DataMember(Name = "scDatabase", EmitDefaultValue = false)]
        public string ScDatabase { get; set; }

        [DataMember(Name = "processes", EmitDefaultValue = false)]
        public List<string> Processes { get; set; }

        [DataMember(Name = "scAppNames", EmitDefaultValue = false)]
        public List<string> ScAppNames { get; set; }

        [DataMember(Name = "starcounterBinDirectory", EmitDefaultValue = false)]
        public string StarcounterBinDirectory { get; set; }

        [DataMember(Name = "overrideRecoveryExecutionTimeout", EmitDefaultValue = false)]
        public uint OverrideRecoveryExecutionTimeout { get; set; }

        public static RecoveryConfigItem Parse(FileInfo fi)
        {
            return Parse<RecoveryConfigItem>(fi);
        }

        public static RecoveryConfigItem Parse(string str)
        {
            return Parse<RecoveryConfigItem>(str);
        }

        public static RecoveryConfigItem Parse(FileStream stream)
        {
            return Parse<RecoveryConfigItem>(stream);

        }

        protected override void OnDeserialized()
        {
            if (Processes == null) Processes = new List<string>();
            if (ScAppNames == null) ScAppNames = new List<string>();
        }
    }
}
