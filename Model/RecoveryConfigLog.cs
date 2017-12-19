using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;

namespace CustomWatchdog
{
    /// <summary>
    /// Defines a log to use
    /// </summary>
    [DataContract]
    public class RecoveryConfigLog : JsonData
    {
        public Accessor Enums => new Accessor(this);


        [DataMember(Name = "type", IsRequired = true)]
        public string LogType { get; set; }

        [DataMember(Name = "level", EmitDefaultValue = false)]
        public string Level { get; set; } = ServiceLogLevel.Info.ToString();

        [DataMember(Name = "path", EmitDefaultValue = false)]
        public string Path { get; set; }



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

        public class Accessor
        {
            private readonly RecoveryConfigLog m_owner;

            public Accessor(RecoveryConfigLog owner)
            {
                m_owner = owner;
            }

            public ServiceLogType LogType
            {
                get
                {
                    var t =  m_owner.LogType;
                    return (ServiceLogType)Enum.Parse(typeof(ServiceLogType), t);
                }
                set
                {
                    m_owner.LogType = value.ToString();
                }
            }

            public ServiceLogLevel Level
            {
                get
                {
                    var t = m_owner.Level;

                    if (string.IsNullOrWhiteSpace(t))
                    {
                        return ServiceLogLevel.Info;
                    }
                    return (ServiceLogLevel)Enum.Parse(typeof(ServiceLogLevel), t);
                }
                set
                {
                    m_owner.Level = value.ToString();
                }
            }
        }
    }
}
