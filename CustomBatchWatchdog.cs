using System.IO;
using System.ServiceProcess;
using System.Diagnostics;
using System.Collections.Generic;
using System;
using System.Threading;
using Toolkit;

namespace CustomWatchdog {

   public partial class CustomBatchWatchdog : ServiceBase {

      const int sleepTime = 500;    // time between consequitive checks
      const int bufferTime = 500;   // time to let whole suite started
      string healingBatch = "";
      const string fileName = "watchprocs.cfg";
      List<string> procNames = new List<string>();

      private void LoadFile () {
         try {
            using (StreamReader file = new StreamReader(fileName)) {
               string line;
               healingBatch = file.ReadLine();
               while ((line = file.ReadLine()) != null) {
                  procNames.Add(line);
               }
               file.Close();
            }
         } catch (IOException) {
            throw new Exception("Problem reading config file " + fileName);
         }
      }

      private void Recover () {
         //System.Diagnostics.Process.Start(healingBatch);
         ApplicationLoader.PROCESS_INFORMATION procInfo;
         ApplicationLoader.StartProcessAndBypassUAC(healingBatch, out procInfo);
      }

      private bool Check () {
         Process[] processlist = Process.GetProcesses();
         foreach (string procName in procNames) {
            bool found = false;
            foreach (Process theprocess in processlist) {
               if (theprocess.ProcessName.Equals(procName)) {
                  found = true;
                  break;
               }
            }
            if (!found) {
               return false;
            }
         }
         return true;
      }

      private void RunForever () {
         while (true) {
            if (!Check()) {
               Recover();
               Thread.Sleep(bufferTime);
            } else {
               Thread.Sleep(sleepTime);
            }
         }
      }

      protected override void OnStart (string[] args) {
         LoadFile();
         ThreadPool.QueueUserWorkItem(o => { RunForever(); });

      }

      public CustomBatchWatchdog () { InitializeComponent(); }
      protected override void OnStop () { }
   }
}
