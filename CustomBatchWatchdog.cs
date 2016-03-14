using System.IO;
using System.ServiceProcess;

namespace CustomWatchdog {

   public partial class CustomBatchWatchdog : ServiceBase {

      public CustomBatchWatchdog () { InitializeComponent(); }

      protected override void OnStart (string[] args) {
         FileStream fs = new FileStream (
            @"c:\CustomWatchdogLog.txt",
            FileMode.OpenOrCreate, FileAccess.Write);
         StreamWriter m_streamWriter = new StreamWriter (fs);
         m_streamWriter.BaseStream.Seek (0, SeekOrigin.End);
         m_streamWriter.WriteLine("CustomBatchWatchdog: Service Started \n");
         m_streamWriter.Flush ();
         m_streamWriter.Close ();
      }

      protected override void OnStop () {
         FileStream fs = new FileStream (
            @"c:\CustomWatchdogLog.txt",
            FileMode.OpenOrCreate, FileAccess.Write);
         StreamWriter m_streamWriter = new StreamWriter (fs);
         m_streamWriter.BaseStream.Seek (0, SeekOrigin.End);
         m_streamWriter.WriteLine ("CustomBatchWatchdog: Service Stopped \n");
         m_streamWriter.Flush ();
         m_streamWriter.Close ();
      }
   }
}
