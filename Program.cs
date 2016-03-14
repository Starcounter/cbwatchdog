using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace CustomWatchdog {
   static class Program {
      static void Main () {
         ServiceBase[] ServicesToRun;
         ServicesToRun = new ServiceBase[]
         {
            new CustomBatchWatchdog()
         };
         ServiceBase.Run(ServicesToRun);
      }
   }
}
