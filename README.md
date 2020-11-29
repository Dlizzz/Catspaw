# Catspaw
A .Net Framework C# application to automate the Catsnet HTPC

An application is running on the HTPC to synchronize HTPC components power state (PC, AVT & TV). When the PC goes to sleep, AVR and TV are turned off and when the PC wakes up, AVR and TV are turned on. Communication with the AVR is done through Http and communication with the TV is done through CeC bus. 
The application implements also an APIserver (http) to execute command (like poweroff). This API is used by a proxy (running on the gateway) which relay commands coming from IFTTT (and Google Assistant).
