/*
 * {
  "healthCheckInterval": "10000",
  "recoveryExecutionTimeout": "300000",
  "noConsoleForRecoveryScript": "false",
  "criticalCounts": "10",
  "recoveryItems": [
    {
      "recoveryBatch": "cbwatchdog_defaultDb.bat",
      "scDatabase": "ScDatabase1",
      "processes": ["scdata"],
      "scAppNames": ["ScApp10", "ScApp11"]
    },
    {
      "recoveryBatch": "cbwatchdog_anotherDb.bat",
      "scDatabase": "ScDatabase2",
      "processes": ["scdata"],
      "scAppNames": ["ScApp20", "ScApp21"]
    }
  ]
}
 */
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System;

namespace CustomWatchdog
{
    [DataContract]
    public class RecoveryConfig : JsonData
    {
        [DataMember(Name = "healthCheckInterval")]
        public uint HealthCheckInterval { get; set; } = 10000;

        [DataMember(Name = "recoveryExecutionTimeout")]
        public uint RecoveryExecutionTimeout { get; set; } = 60000 * 5;

        [DataMember(Name = "noConsoleForRecoveryScript")]
        public bool NoConsoleForRecoveryScript { get; set; }


        [DataMember(Name = "criticalCounts")]
        public uint CriticalCounts { get; set; } = 10;

        [DataMember(Name = "recoveryItems", EmitDefaultValue = false)]
        public List<RecoveryConfigItem> RecoveryItems { get; set; } = new List<RecoveryConfigItem>();

        /// <summary>
        /// User defined logs
        /// </summary>
        [DataMember(Name = "logs", EmitDefaultValue = false)]
        public List<RecoveryConfigLog> Logs { get; set; } = new List<RecoveryConfigLog>();


        /// <summary>
        /// Throws an exception if misconfigured
        /// </summary>
        public void Validate()
        {
            Validator.Check(this);
        }

        public static RecoveryConfig Parse(FileInfo fi)
        {
            return Parse<RecoveryConfig>(fi);
        }

        public static RecoveryConfig Parse(string str)
        {
            return Parse<RecoveryConfig>(str);
        }

        public static RecoveryConfig Parse(FileStream stream)
        {
            return Parse<RecoveryConfig>(stream);

        }

        protected override void OnDeserialized()
        {
            if (RecoveryItems == null) RecoveryItems = new List<RecoveryConfigItem>();
            if (Logs == null) Logs = new List<RecoveryConfigLog>();
        }


        private class Validator : IEqualityComparer<RecoveryConfigItem>
        {
            private readonly RecoveryConfig m_owner;

            public Validator(RecoveryConfig recoveryConfig)
            {
                this.m_owner = recoveryConfig;
            }

            internal static void Check(RecoveryConfig recoveryConfig)
            {
                var v = new Validator(recoveryConfig);
                v.DoCheck();
            }

            private void DoCheck()
            {
                var items = m_owner.RecoveryItems;

                if (items != null)
                {
                    var hashSet = new HashSet<string>();

                    foreach (var item in items)
                    {
                        var b = item.RecoveryBatch;

                        if (string.IsNullOrWhiteSpace(b))
                        {
                            throw new ApplicationException($"Invalid configuration, batch file is required{Environment.NewLine}{item}");
                        }
                        else
                        {
                            var key = b.ToLowerInvariant();

                            if (hashSet.Contains(key))
                            {
                                throw new ApplicationException($"Invalid configuration, batch file is already used: {b}{Environment.NewLine}{m_owner}");
                            }
                            hashSet.Add(key);
                        }
                    }
                }
            }

            public bool Equals(RecoveryConfigItem x, RecoveryConfigItem y)
            {
                return string.Equals(x.RecoveryBatch, y.RecoveryBatch, StringComparison.InvariantCultureIgnoreCase);
            }

            public int GetHashCode(RecoveryConfigItem obj)
            {
                var b = obj.RecoveryBatch;
                return b != null ? b.GetHashCode() : -1;
            }
        }
    }
}
