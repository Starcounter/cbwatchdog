using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Toolkit
{
    class ApplicationInlineLoader
    {
        internal static Process Start(string recoveryBatch, bool noConsoleForRecoveryScript, uint timeout, Action<string> printInfo)
        {
            var p = new Process();
            var psi = p.StartInfo;
            psi.FileName = recoveryBatch;
            psi.WorkingDirectory = Path.GetDirectoryName(recoveryBatch);
            psi.UseShellExecute = !noConsoleForRecoveryScript;
            psi.CreateNoWindow = !noConsoleForRecoveryScript;

            try
            {
                p.Start();
                try
                {
                    printInfo($"Inline process: {p.ProcessName}, pid: {p.Id}");
                }
                catch
                {
                }
                return p;
            }
            catch (Exception ex)
            {
                printInfo($"Inline Recovery execution within execution timeout \"recoveryExecutionTimeout={timeout}\" for file {recoveryBatch} FAILED, {ex}");
            }
            return null;
        }
    }
}
