using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace CustomWatchdog
{
    /// <summary>
    /// A helper file for dealing with Json operations
    /// </summary>
    [DataContract]
    public abstract class JsonData
    {
        public sealed override string ToString()
        {
            try
            {
                using (var ms = new MemoryStream())
                {
                    Serialize(ms);
                    return Encoding.Unicode.GetString(ms.ToArray());
                }
            }
            catch
            {

            }
            return $"ToString Failed: {base.ToString()}";
        }

        /// <summary>
        /// Parses the content of the given file into an instance of T. T must be a DataContract
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="fi"></param>
        /// <returns></returns>
        protected static T Parse<T>(FileInfo fi)
            where  T : JsonData
        {
            using (var stream = fi.Open(FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return Parse<T>(stream);
            }
        }

        /// <summary>
        /// Parses an instance of T from the stream. T must be a DataContract
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="fi"></param>
        /// <returns></returns>
        public static T Parse<T>(Stream stream)
            where T : JsonData
        {
            DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(T));
            return (T)ser.ReadObject(stream);
        }

        /// <summary>
        /// Parses the string into an instance of T. T must be a DataContract
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="fi"></param>
        /// <returns></returns>
        public static T Parse<T>(string str)
            where T : JsonData
        {
            DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(T));

            using (var stream = new MemoryStream(Encoding.Unicode.GetBytes(str)))
            {
                return (T)ser.ReadObject(stream);
            }
        }

        public void Save(FileInfo fi)
        {
            var di = fi.Directory;

            if (!di.Exists)
            {
                di.Create();
            }
            using (var stream = fi.Open(FileMode.Create, FileAccess.ReadWrite, FileShare.Read))
            {
                Serialize(stream);
            }
        }

        /// <summary>
        /// Serializes the object into the stream using DataContract
        /// </summary>
        /// <param name="fi"></param>
        /// <returns></returns>
        public void Serialize(Stream stream)
        {
            var type = GetType();

            using (var writer = JsonReaderWriterFactory.CreateJsonWriter(stream, Encoding.Unicode, false, true))
            {
                var ser = new DataContractJsonSerializer(type);
                ser.WriteObject(writer, this);

            }
        }
    }
}
