# ENET WiFi Adapter
For the BMW F-models there is a WiFi adapter available, that allows to communicate directly using the BMW ENET protocol.  
It is based on the hardware of an [A5-V11 3G/4G Router](https://wiki.openwrt.org/toh/unbranded/a5-v11). The adapter has the following features:
* MediaTek/Ralink RT5350F processor with 350MHz
* DC/DC converter for improved power consumption
* With additional CPU heat sink for over temperature protection
* [OpenWrt](https://openwrt.org/) operating system
* [LuCi](http://luci.subsignal.org/trac) web interface
* Firmware update possible via web interface
* DHCP server
* ESSID: Deep OBD BMW
* Default Wifi password: deepobdbmw
* Default IP address: 192.168.100.1
* Default root password: root
* The adapter has been redesigned with a smaller housing.

![ENET adapter open](ENET_WiFi_Adapter_EnetAdapter2OpenSmall.png)

![ENET adapter closed](ENET_WiFi_Adapter_EnetAdapter2ClosedSmall.png) ![Web interface](ENET_WiFi_Adapter_WebInterfaceSmall.png) 

## Alternative WiFi adapter
There is alternative commercial [Unichip ENET WiFi adapter](http://www.unichip-tec.com/bmw_enet_wifi_wireless_esys.html) available.  
It's recommended to changed the settings of the adapter to the values of the `EnetWifiSettings.dat` configuration file.
You could buy the proconfigured [ENET WiFi adapter](https://www.ebay.de/itm/254216943413) at EBAY.  
For BMW pre F-models use the [Bluetooth adapter](Replacement_firmware_for_ELM327.md)

## Adapter cable
You could also use an ENET adapter cable, in this case you have to configure the Android LAN adapter with an Auto IP address,
for example `169.254.1.10 / 255.255.0.0`.  
This is required because the vehicle will not get a DHCP address and will fall back to Auto IP adress mode.

## Factory reset
If the adapter gets unreachable after a misconfiguration there is a possibility to perform a factory reset.  
You have to open the adapter and press the reset button after the adapter has booted.

## Use the adapter with INPA, Tool32 or ISTA-D
You could use the Bluetooth adapter on a windows PC with INPA, Tool32 or ISTA-D as a replacement for an ENET adapter cable. The following steps are required to establish the connection:
* Install [.NET framework 4.0](https://www.microsoft.com/de-de/download/details.aspx?id=17718) or higher and [VS2015 C++ runtime](https://www.microsoft.com/de-de/download/details.aspx?id=48145) (recommended, but not required)
* Optionally connect the ENET adapter with the PC. The PC automatically gets an IP address from the adapter DHCP server.
* Download the [latest binary](https://github.com/uholeschak/ediabaslib/releases/latest) package and extract the .zip file. Start `Api32\EdiabasLibConfigTool.exe` and follow the instructions in the status window: Search the adapter, select it, optionally click `Connect`, click `Check Connection` and patch the required EDIABAS installations.
* For ISTA-D: You have to select the `EDIABAS\bin` directory inside ISTA-D first.
* Optionally you could also open the adapter configuration page in the web browser.
* For ISTA-D: In `Administration` -> `VCI Config` select as `Interface type`: `Ediabas default settings (ediabas.ini)`

![EdiabasLib Config Tool](ENET_WiFi_Adapter_ConfigToolWiFiSmall.png)
