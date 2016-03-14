namespace CustomWatchdog {
   partial class CustomBatchWatchdog {
      private System.ComponentModel.IContainer components = null;
      protected override void Dispose (bool disposing) {
         if (disposing && (components != null)) {
            components.Dispose();
         }
         base.Dispose(disposing);
      }
      #region Component Designer generated code
      private void InitializeComponent () {
         components = new System.ComponentModel.Container();
         this.ServiceName = "Custom Batch Watchdog";
      }
      #endregion
   }
}
