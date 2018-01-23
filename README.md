# Custom Batch Watchdog (```cbwatchdog```)

The Windows system service ```Custom Batch Watchdog``` (```cbwatchdog```) watches a list of processes to be in-place and running, and executes a batch file if one or more are missing. Service is configured via ```cbwatchdog.json``` which must be put into ```%windir%/System32``` (which usually is ```C:/windows/system32```). The service is essentially a simple state machine. Every ```healthCheckInterval``` milliseconds it checks whether all the appplications with names from a list ```processes``` are presented. If one or more are not running at a moment of check, the service executes ```recoveryBatch```. After syncronously running ```recoveryBatch``` service starts to check at every ```recoveryPauseInterval``` milliseconds whether the whole list of apps is back again. It checks it in a loop for at most ```criticalCounts``` times, waiting ```recoveryPauseInterval``` milliseconds each time, and, if waiting after ```criticalCounts``` times is still unsucessful, it executes ```recoveryBatch``` again, and the loop repeats.

## Installing the service

![](./docs/WatchDoge.jpg)

*Step 1: Download* 

-Download the lastest release binaries from the [Git Repository](https://github.com/Starcounter/cbwatchdog/releases).

*Step 2: To copy executable files * 
 
- Copy the following files into the folder where you intend to have the service running on your computer:
	* cbwatchdog.exe
	* cbwatchdog.json

*Step 3: To register the service * 

 - Run PowerShell as Administrator.
 - Change the current directory to the directory where you copied the executables files in previous step.
 - Run the following command to install the service:
	```installutil cbwatchdog.exe```
	It should successfully install the service.
	
*Step 4: To configure the service * 
	
 - In `Run window` enter following command: 
	```services.msi```
  Following window will open:
  
  ![](./docs/Service-Start.png)
  
 - Locate the service `Custom Batch Watchdog`
 - Open properties window by right-clicking on the service name.
 - Change the startup type to `Automatic` to start the service on system logon.
 
	![](./docs/Service-Start-2.png)

 
 ## Running the service
 
 *Step 1: To start the service*
 
 - Open PowerShell window.
 - Run the following command to run the service:
 
	```net start CustomBatchWatchdog /"C:\Watchdog\cbwatchdog.json"```
	
 where the `C:\Watchdog\cbwatchdog.json` is the json configuration file path on your computer.
 
 It should open the `Notepad`, try closing it and see what happens.
 


## Diagnostics

The watchdog writes all events to a system log. Here is what you typically see via Windows Event Viewer:

![](./docs/Service-Start-3.png)

## Defaults and obligatory options

The full structure of ```cbwatchdog.json``` is as following:

```json
{
  "healthCheckInterval": "10000",
  "recoveryExecutionTimeout": "300000",
  "noConsoleForRecoveryScript": "false",
  "criticalCounts": "10",
  "recoveryItems": [
    {
      "recoveryBatch": "cbwatchdog.bat",
      "scDatabase": "default",
      "overrideRecoveryExecutionTimeout": "10000",
      "processes": ["ProcessName"],
      "scAppNames": ["ApplicationName"]
    }
  ]
}
```

where some properties are optional and are provided with the following default values:

```csharp
int healthCheckInterval = 10000; // number of milliseconds waiting between each check
int recoveryExecutionTimeout = 300000; // number of milliseconds before recoveryBatch is timeed out
int criticalCounts = 10; // number of times recoveryBatch may be executed in a row
bool noConsoleForRecoveryScript = false; // true: Show console for recoveryBatch, false: Do not show console for recoveryBatch
```

```recoveryItems``` is an array in order to be able to monitor several different Starcounter databases and have unique ```recoveryBatch``` files. If any of the following are not running, then ```recoveryBatch``` will be executed.
```overrideRecoveryExecutionTimeout``` overrides the ```recoveryExecutionTimeout``` for the recovery item if specified.
* ```processes```: Observing if these processes are running
* ```scAppNames```: Observing if these apps are running in the target ```"scDatabase"``` Starcounter database

The array ```scAppNames``` of Starcounter applications are evaluated by parsing the output from ```staradmin.exe```:

```csharp
// stdOutput is output from `staradmin --database={scDatabase} list app`
bool allAppsAreRunning = scAppNames.All(appName => stdOutput.Contains($"{appName} (in {scDatabase})"));
```

## Uninstallation

Open Admin-rights command prompt, go to your service exe location and fire ```installutil /u cbwatchdog.exe```. A faster way is to type: ```sc delete "CustomBatchWatchdog"```.
