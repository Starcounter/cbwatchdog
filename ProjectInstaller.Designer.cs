namespace CustomWatchdog {
   partial class ProjectInstaller {
      /// <summary>
      /// Required designer variable.
      /// </summary>
      private System.ComponentModel.IContainer components = null;

      /// <summary> 
      /// Clean up any resources being used.
      /// </summary>
      /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
      protected override void Dispose (bool disposing) {
         if (disposing && (components != null)) {
            components.Dispose();
         }
         base.Dispose(disposing);
      }

      #region Component Designer generated code

      /// <summary>
      /// Required method for Designer support - do not modify
      /// the contents of this method with the code editor.
      /// </summary>
      private void InitializeComponent () {
         this.serviceProcessInstaller = new System.ServiceProcess.ServiceProcessInstaller();
         this.CustomBatchWatchdog = new System.ServiceProcess.ServiceInstaller();
         // 
         // serviceProcessInstaller
         // 
         this.serviceProcessInstaller.Account = System.ServiceProcess.ServiceAccount.LocalSystem;
         this.serviceProcessInstaller.Password = null;
         this.serviceProcessInstaller.Username = null;
         this.serviceProcessInstaller.AfterInstall += new System.Configuration.Install.InstallEventHandler(this.serviceProcessInstaller1_AfterInstall);
         // 
         // CustomBatchWatchdog
         // 
         this.CustomBatchWatchdog.Description = "Watches a set of given apps and runs the batch file if one is suddenly missing";
         this.CustomBatchWatchdog.DisplayName = "Custom Batch Watchdog";
         this.CustomBatchWatchdog.ServiceName = "Custom Batch Watchdog";
         this.CustomBatchWatchdog.AfterInstall += new System.Configuration.Install.InstallEventHandler(this.serviceInstaller1_AfterInstall);
         // 
         // ProjectInstaller
         // 
         this.Installers.AddRange(new System.Configuration.Install.Installer[] {
            this.serviceProcessInstaller,
            this.CustomBatchWatchdog});

      }

      #endregion

      private System.ServiceProcess.ServiceProcessInstaller serviceProcessInstaller;
      private System.ServiceProcess.ServiceInstaller CustomBatchWatchdog;
   }
}