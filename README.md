# Custom Batch Watchdog (```cbwatchdog```)

The Windows system service ```Custom Batch Watchdog``` (```cbwatchdog```) watches a list of processes to be in-place and running, and executes a batch file if one or more are missing. Service is configured via ```cbwatchdog.json```. The service is essentially a simple state machine. Every ```healthCheckInterval``` milliseconds it checks whether all the appplications with names from a list ```processes``` are presented. If one or more are not running at a moment of check, the service executes ```recoveryBatch```. After syncronously running ```recoveryBatch``` service starts to check at every ```recoveryExecutionTimeout``` milliseconds whether the whole list of apps is back again. It checks it in a loop for at most ```criticalCounts``` times, waiting ```recoveryExecutionTimeout``` milliseconds each time, and, if waiting after ```criticalCounts``` times is still unsucessful, it executes ```recoveryBatch``` again, and the loop repeats.

## Installing the service

![](./docs/WatchDoge.jpg)

*Step 1: Download* 

- Download the lastest release binaries from the [Git Repository](https://github.com/Starcounter/cbwatchdog/releases).

*Step 2: To copy executable files* 
 
- Copy the following files into the folder where you intend to have the service running on your computer:
	* cbwatchdog.exe
	* cbwatchdog.json
	* cbwatchdog.bat

*Step 3: To register the service* 

 - Run `PowerShell` as `Administrator`.
 - Change the current directory to the directory where you copied the executables files in previous step.
 - Run the following command to install the service:
 
	```	
	installutil cbwatchdog.exe	
	```
	
	It should successfully install the service.
	
*Step 4: To configure the service* 
	
 - In `Run window` enter following command: 
 
	```
	services.msi
	```
	
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
 
	```
	net start CustomBatchWatchdog /"C:\Watchdog\cbwatchdog.json"
	```
	
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
      "starcounterBinDirectory": "C:\\Program Files\\Starcounter",
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

* ```recoveryBatch```: Full path for the batch file that needs to be executed, for example `C:\\Watchdog\\cbwatchdog.bat`
* ```starcounterBinDirectory```: Directory where star counter is installed.
* ```processes```: Name of processes that need to be observed, to make sure they are running. Example ```["scData"]```
* ```scAppNames```: Name of the Apps that need to be observed to ensure they are always running in the target ```"scDatabase"``` Starcounter database. Example ```["App3","App2"]```

The array ```scAppNames``` of Starcounter applications are evaluated by parsing the output from ```staradmin.exe```:

```csharp
// stdOutput is output from `staradmin --database={scDatabase} list app`
bool allAppsAreRunning = scAppNames.All(appName => stdOutput.Contains($"{appName} (in {scDatabase})"));
```

## Configuring for Starcounter Custom Applications. 

The `cbwatchdog` service can be configured to monitor Starcounter Custom Applications and the required services are always up and running if they get stopped accidentally.

*Step 1: Creating a `.bat` file* 

Create a `.bat` file with the commands that starts a Starcounter Database and the applications that using that Database. Following is a sample `.bat` file:

```
echo off

staradmin -d=DB1 start server :: Command that starts a database and the server.

star -d=DB1 E:\Work\Starcounter\App1\App1\bin\Debug\App1.exe :: Command that hosts an application inside a Database server.
star -d=DB1 E:\Work\Starcounter\App2\App2\bin\Debug\App2.exe :: -do-

```

Full list of commands that you can use with Starcounter Application Framework can be found [here](https://docs.starcounter.io/guides/working-with-starcounter/star-cli)

*Step 2: Modifying the`cbwatchdog.json` file* 

Modify the config file to use the `.bat` file created in previous step. Following is a sample `cbwatchdog.config` file:

```json
{
  "healthCheckInterval": "10000",
  "recoveryExecutionTimeout": "300000",
  "noConsoleForRecoveryScript": "false",
  "criticalCounts": "10",
  "recoveryItems": [
    {
      "recoveryBatch": "C:\\Watchdog\\cbwatchdog.bat", 
      "scDatabase": "DB1",
      "overrideRecoveryExecutionTimeout": "10000",
      "starcounterBinDirectory": "C:\\Program Files\\Starcounter", 
      "processes": ["scData"],
      "scAppNames": ["App1","App2"]
    }
  ]
}

```
#### What happens under the hood

The `cbwatchdog` service monitors the `scDatabase`, `processes` and `scAppNames` after every `healthCheckInterval` milliseconds. If any of these services or apps found missing on the computer it just runs the 
`.bat` file mentioned in `recoveryBatch` inside the `cbwatchdog.json` config file.

## Uninstallation

*Step 1: To start the service*
 
 - Open PowerShell window with Admin-rights.
 - Run the following command to run the service:
 
	```
	installutil /u cbwatchdog.exe
	```
 A faster way is to type:
 
 ```
 sc delete "CustomBatchWatchdog"
 ```
