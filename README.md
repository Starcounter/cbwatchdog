# Custom Batch Watchdog

![](WatchDoge.jpg)

The system service ```Custom Batch Watchdog``` watches a list of processes to be in-place running, and executes a batch file if one or more are missing. Service is configured via ```cbwatchdog.json``` which must be put into ```%windir%/System32``` (which usually is ```C:/windows/system32```). The service is essentially a simple state machine. Every ```healthCheckInterval``` milliseconds it checks whether all the appplications with names from a list ```processes``` are presented. If one or more are not running at a moment of check, the service executes ```recoveryBatch```. After syncronously running ```recoveryBatch``` service starts to check at every ```recoveryPauseInterval``` milliseconds whether the whole list of apps is back again. It checks it in a loop for at most ```criticalCounts``` times, waiting ```recoveryPauseInterval``` milliseconds each time, after which it executes ```recoveryBatch``` again, and the loop repeats.

### How to setup

1. Compile a tool from sources. It is a C# system service. Do not run it.
2. Prepare the sample configuration. This configuration simply keeps ```notepad``` run forever. This is the most annoying thing that may ever happen to you! So, just copy ```cbwatchdog.json``` and ```cbwatchdog.bat``` to ```%windir%/System32``` (which usually is ```C:/windows/system32```).
2. For instance we compiled it to ```c:\Dan\CustomWatchdog\bin\Release\```.

### Diagnostics
