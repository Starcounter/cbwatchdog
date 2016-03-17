# Custom Batch Watchdog

The system service ```Custom Batch Watchdog``` watches a list of processes to be in-place running, and executes a batch file if one or more are missing. Service is configured via ```cbwatchdog.json``` which must be put into ```%windir%/System32``` (which usually is ```C:/windows/system32```). The service is essentially a simple state machine. Every ```healthCheckInterval``` milliseconds it checks whether all the appplications with names from a list ```processes``` are presented. If one or more are not running at a moment of check, the service executes ```recoveryBatch```. After syncronously running ```recoveryBatch``` service starts to check at every ```recoveryPauseInterval``` milliseconds whether the whole list of apps is back again. It checks it in a loop for at most ```criticalCounts``` times, waiting ```recoveryPauseInterval``` milliseconds each time, after which it executes ```recoveryBatch``` again, and the loop repeats.

![](WatchDoge.jpg)

### How to setup

1. Compile a tool from sources. It is a C# system service. Do not run it from Visual Studio.
2. Prepare the sample configuration. This configuration bundled with source code simply keeps ```notepad``` run forever. This is the most annoying thing that may ever happen in your life! So, just copy ```cbwatchdog.json``` and ```cbwatchdog.bat``` to ```%windir%/System32``` (which usually is ```C:/windows/system32```).
3. For instance, we compiled the code to ```c:\Dan\CustomWatchdog\bin\Release\```. Open command prompt with Admin rights ("Start" -> cmd -> Right Click -> "Run as Administrator") and navigate to this directory (```cd c:\Dan\CustomWatchdog\bin\Release```).
4. Install ```cbwatchdog.exe``` as a service with a command: ```installutil cbwatchdog.exe```

### Diagnostics
