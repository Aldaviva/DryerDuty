<p align="center"><a href="https://github.com/Aldaviva/LaundryDuty">Washing Machine</a> &middot; <strong>Dryer</strong></p>

DryerDuty
===

[![GitHub Workflow Status](https://img.shields.io/github/actions/workflow/status/Aldaviva/DryerDuty/dotnet.yml?branch=master&logo=github)](https://github.com/Aldaviva/DryerDuty/actions/workflows/dotnet.yml) [![Testspace](https://img.shields.io/testspace/tests/Aldaviva/Aldaviva:DryerDuty/master?passed_label=passing&failed_label=failing&logo=data%3Aimage%2Fsvg%2Bxml%3Bbase64%2CPHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCA4NTkgODYxIj48cGF0aCBkPSJtNTk4IDUxMy05NCA5NCAyOCAyNyA5NC05NC0yOC0yN3pNMzA2IDIyNmwtOTQgOTQgMjggMjggOTQtOTQtMjgtMjh6bS00NiAyODctMjcgMjcgOTQgOTQgMjctMjctOTQtOTR6bTI5My0yODctMjcgMjggOTQgOTQgMjctMjgtOTQtOTR6TTQzMiA4NjFjNDEuMzMgMCA3Ni44My0xNC42NyAxMDYuNS00NFM1ODMgNzUyIDU4MyA3MTBjMC00MS4zMy0xNC44My03Ni44My00NC41LTEwNi41UzQ3My4zMyA1NTkgNDMyIDU1OWMtNDIgMC03Ny42NyAxNC44My0xMDcgNDQuNXMtNDQgNjUuMTctNDQgMTA2LjVjMCA0MiAxNC42NyA3Ny42NyA0NCAxMDdzNjUgNDQgMTA3IDQ0em0wLTU1OWM0MS4zMyAwIDc2LjgzLTE0LjgzIDEwNi41LTQ0LjVTNTgzIDE5Mi4zMyA1ODMgMTUxYzAtNDItMTQuODMtNzcuNjctNDQuNS0xMDdTNDczLjMzIDAgNDMyIDBjLTQyIDAtNzcuNjcgMTQuNjctMTA3IDQ0cy00NCA2NS00NCAxMDdjMCA0MS4zMyAxNC42NyA3Ni44MyA0NCAxMDYuNVMzOTAgMzAyIDQzMiAzMDJ6bTI3NiAyODJjNDIgMCA3Ny42Ny0xNC44MyAxMDctNDQuNXM0NC02NS4xNyA0NC0xMDYuNWMwLTQyLTE0LjY3LTc3LjY3LTQ0LTEwN3MtNjUtNDQtMTA3LTQ0Yy00MS4zMyAwLTc2LjY3IDE0LjY3LTEwNiA0NHMtNDQgNjUtNDQgMTA3YzAgNDEuMzMgMTQuNjcgNzYuODMgNDQgMTA2LjVTNjY2LjY3IDU4NCA3MDggNTg0em0tNTU3IDBjNDIgMCA3Ny42Ny0xNC44MyAxMDctNDQuNXM0NC02NS4xNyA0NC0xMDYuNWMwLTQyLTE0LjY3LTc3LjY3LTQ0LTEwN3MtNjUtNDQtMTA3LTQ0Yy00MS4zMyAwLTc2LjgzIDE0LjY3LTEwNi41IDQ0UzAgMzkxIDAgNDMzYzAgNDEuMzMgMTQuODMgNzYuODMgNDQuNSAxMDYuNVMxMDkuNjcgNTg0IDE1MSA1ODR6IiBmaWxsPSIjZmZmIi8%2BPC9zdmc%2B)](https://aldaviva.testspace.com/spaces/223055) [![Coveralls](https://img.shields.io/coveralls/github/Aldaviva/DryerDuty?logo=coveralls)](https://coveralls.io/github/Aldaviva/DryerDuty?branch=master)

*Notify you when your dryer has finished a load of laundry by sending a PagerDuty alert.*

<!-- MarkdownTOC autolink="true" bracket="round" autoanchor="true" levels="1,2" bullets="1.,-,-,-" -->

1. [Behavior](#behavior)
1. [Prerequisites](#prerequisites)
1. [Circuit diagrams](#circuit-diagrams)
1. [Installation](#installation)
1. [Configuration](#configuration)
1. [Running](#running)
1. [References](#references)

<!-- /MarkdownTOC -->

![Dryer timer dial](.github/images/header.jpg)

<a id="behavior"></a>
## Behavior

1. When you start a load of laundry in the dryer, an induction clamp sensor installed inside the dryer detects the increased current flowing from the start button to the motor.
1. A .NET daemon running on a Raspberry Pi reads the voltage from the induction sensor using an analog-to-digital converter, and sends a Change event to PagerDuty when the motor starts.
1. When the motor stops, the Raspberry Pi triggers an Alert in PagerDuty. This will notify you on your configured communications channels, like a push notification in the mobile app.
1. When you open the dryer door to remove the laundry, another induction clamp sensor detects the door light turning on, and the Raspberry Pi automatically resolves the Alert so you don't keep getting notifications.

<a id="prerequisites"></a>
## Prerequisites
- [Raspberry Pi 2 Model B rev 1.1](https://www.raspberrypi.com/products/) or later
    - [Raspberry Pi OS Lite 11](https://www.raspberrypi.com/software/operating-systems/) or later
    - [USB Wi-Fi adapter](https://www.canakit.com/raspberry-pi-wifi.html), unless you have a Raspberry Pi 3 or newer with built-in Wi-Fi
    - A micro-USB AC adapter with a long enough cable to reach the top of the dryer
- [.NET 7 ARM Runtime](https://dotnet.microsoft.com/en-us/download/dotnet) or later
    - Distribution package archives don't offer ARM packages of .NET, so you have to install it using the [official installation script](https://dotnet.microsoft.com/en-us/download/dotnet/scripts).
        ```sh
        wget https://dotnet.microsoft.com/download/dotnet/scripts/v1/dotnet-install.sh
        sudo bash dotnet-install.sh --channel STS --runtime dotnet --install-dir /usr/share/dotnet/
        sudo ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet
        dotnet --info # make sure the Microsoft.NETCore.App runtime shows up as installed
        rm dotnet-install.sh
        ```
- [PagerDuty account](https://www.pagerduty.com/sign-up/) (the [free plan](https://www.pagerduty.com/sign-up-free/?type=free) is sufficient)
- Clothes dryer
    - [Kenmore 500 series 11065102310 240V 26A electric dryer](https://www.searspartsdirect.com/model/32k35liyt3-000582/kenmore-11065102310-dryer-parts)
    - The door light must be working for this program to detect when the door is opened, so you must replace the bulb if it has burned out.
    - Make sure you use the OEM-style [E12 incandescent door light bulbs](https://www.amazon.com/dp/B07XNPL2RW) instead of [LED replacements](https://www.amazon.com/dp/B08K32T7Y2), because the LEDs don't draw enough current to be easily detectable.
- Current sensor circuit [(DigiKey shared cart)](https://www.digikey.com/short/8288b5q3)
    - [YHDC 60A voltage output current sensing clamp transformer](https://www.digikey.com/en/products/detail/seeed-technology-co-ltd/101990064/5487440) to non-invasively measure the motor current and output a proportional voltage in the range [0,1] V
    - [YHDC 5A voltage output current sending clamp transformer](https://www.digikey.com/en/products/detail/seeed-technology-co-ltd/101990058/5487435) for the door light current which is much less than the motor current
    - [MCP3008 I/P 10-bit Analog-to-Digital Converter](https://www.digikey.com/en/products/detail/microchip-technology/MCP3008-I-P/319422) to convert the analog [0,1] V signal from the current transformers into a digital [0,1023] value and send it over an SPI connection to the Raspberry Pi, which does not have its own built-in ADC
    - 2 × [3.5mm TRS jacks](https://www.digikey.com/en/products/detail/kycon-inc/STX-3120-3B/9990113) to connect the current transformers to the rest of the circuit
    - 4 × [100kΩ 0.5W resistors](https://www.digikey.com/en/products/detail/yageo/CFR50SJT-52-100K/9099700) for the voltage dividers, which add voltage to the transformer signals so the unsigned ADC won't clip negative values to 0
    - 2 × [10μF 50V aluminum electrolytic capacitors](https://www.digikey.com/en/products/detail/rubycon/50YXF10MEFCTA5X11/3567102) for the voltage dividers
    - [Breadboard](https://www.digikey.com/en/products/detail/adafruit-industries-llc/64/7241427) or [perf-board](https://www.adafruit.com/product/571)
    - Wires or [jumper](https://www.adafruit.com/product/826) [cables](https://www.adafruit.com/product/758) to connect the components

<a id="circuit-diagrams"></a>
## Circuit diagrams

### Current sensor

<table>
<thead>
<tr>
<th>Visual view</th>
<th>Schematic view</th>
</tr>
</thead>
<tbody>
<tr>
<td><img src="Circuit/Current sensor.svg" alt="current sensor circuit, visual view" /></td>
<td><img src="Circuit/Current sensor schematics.svg" alt="current sensor circuit, schematic view" /></td>
</tr>
<tr>
<td colspan="2" align="center"><a href="Circuit/Current sensor.fzz"><strong>💾 Download Fritzing file</strong></a></td>
</tr>
</tbody>
</table>

### Dryer

<p><img src=".github/images/dryer-wiring-diagram.jpg" alt="Dryer wiring diagram" /></p>

The 60A motor clamp sensor attaches to the light blue wire that connects the Push To Start Relay to the Drive Motor.

The 5A light clamp sensor attaches to the orange wire that connects the NC terminal of the door switch to the drum lamp.

<a id="installation"></a>
## Installation

<a id="hardware"></a>
### Hardware

1. Open the main cabinet of the dryer by unscrewing the two Phillips screws on the lint trap, then prying up on the front edge of the top panel. There are two springs that hold it down on the left and right side. I used a plastic panel puller to lift the lid.
1. Clamp the 5A current transformer around the orange wire that leads to the door switch on the right side of the cabinet.
1. Run the end of the wire with the 3.5mm TRS plug up into the hole in the back center of the lid that leads to the control panel.
1. Close the cabinet lid and replace the two lint trap screws.
1. Open the control panel by pushing straight in (not pulling up) under the left and right side with a panel puller to release the two springs. Pitch the control panel back and rest it on something.
1. Clamp the 60A current transformer around one of the two light blue wires leading to the Start button.
1. Place the Raspberry Pi, connected to the assembled current sensing circuit, underneath the control panel. You may need to stand the Raspberry Pi up on its edge so it will fit.
1. Connect the 3.5mm TRS plug from the 60A motor sensor to the ADC Channel 0 3.5mm jack in your circuit.
1. Connect the 3.5mm TRS plug from the 5A door light sensor to the ADC Channel 1 3.5mm jack in your circuit.
1. Plug the Raspberry Pi in to a micro-USB AC power adapter and run the cable underneath the side of the control panel.
1. Check one final time that you can SSH into the Raspberry Pi.
1. Close the control panel.

<a id="software"></a>
### Software
1. Enable the [SPI](https://www.raspberrypi.com/documentation/computers/raspberry-pi.html#spi-overview) kernel module on your Raspberry Pi using `sudo raspi-config` › `3 Interface Options` › `I4 SPI`, then reboot.
1. Download the [`DryerDuty.zip`](https://github.com/Aldaviva/DryerDuty/releases/latest/download/DryerDuty.zip) file from the [latest release](https://github.com/Aldaviva/DryerDuty/releases/latest) to your Raspberry Pi.
1. Extract the ZIP file to a directory like `/opt/dryerduty/`.
1. Allow the program to be executed by running `chmod +x /opt/dryerduty/DryerDuty`.
1. Install the SystemD service by running
    ```sh
    sudo cp /opt/dryerduty/dryerduty.service /etc/systemd/system/
    sudo systemctl daemon-reload
    sudo systemctl enable dryerduty.service
    ```

<a id="configuration"></a>
## Configuration

<a id="pagerduty"></a>
### PagerDuty

Create an Integration in PagerDuty and get its Integration Key.

1. Sign into your [PagerDuty account](https://app.pagerduty.com/).
1. Go to Services › Service Directory.
1. Select an existing Service for which you want to publish events, or create a new Service.
1. In the Integrations tab of the Service, add a new Integration.
1. Under Most popular integrations, select Events API V2, then click Add.
1. Expand the newly-created Integration and copy its **Integration Key**, which will be used to authorize this program to send Events to the correct Service.

<a id="dryerduty"></a>
### DryerDuty

DryerDuty is configured using `appsettings.json` in the installation directory.

- `pagerDutyIntegrationKey` is the Integration Key that PagerDuty gives you when you create a new Events API v2 Integration for one of your Services.
- `motorMinimumActiveAmps` is the minimum current, in amps, which would indicate that the dryer's motor is running.
    - My dryer's motor runs at 4.33A, so I set this to `2.0`.
- `lightMinimumActiveAmps` is the minmum current, in amps, which would indicate that the light bulb in the drum turned on because the door was opened.
    - My 15W bulb runs at 0.08A, so I set this to `0.04`.
- `motorGain` is a coefficient which the motor current is multiplied by to get a more accurate value.
    - The default value is `1.0`, but I had to set mine to `1.64` to match the current readings from my clamp multimeter.
- `lightGain` is a coefficient which the light bulb current is multiplied by to get a more accurate value.
    - The default value is `1.0`, but I had to set mine to `0.75` to match the nominal current of my bulb.
- `Logging.LogLevel` controls the log verbosity, where the key is the namespace and the value is the [log level](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.loglevel?view=dotnet-plat-ext-7.0) name.
    - To see current readings from this library, set `Logging.LogLevel.DryerDuty` to `Trace` and run `/opt/dryerduty/DryerDuty` from the command line.

<a id="running"></a>
## Running

<a id="starting-the-service"></a>
### Starting the service

```sh
sudo systemctl start dryerduty.service
```

<a id="checking-status"></a>
### Checking status

```sh
sudo systemctl status dryerduty.service
```

<a id="viewing-logs"></a>
### Viewing logs

```sh
sudo journalctl -u dryerduty.service
```

<a id="references"></a>
## References
- [command-tab/brewbot](https://github.com/command-tab/brewbot)
- [SCT-013 Split Core Current Transformer (InnovatorsGuru)](https://innovatorsguru.com/sct-013-000/)
- [MCP3xxx family of Analog to Digital Converters (Microsoft)](https://github.com/dotnet/iot/blob/main/src/devices/Mcp3xxx/README.md)
- Adafruit
    - [Analog Inputs for Raspberry Pi Using the MCP3008](https://learn.adafruit.com/reading-a-analog-in-and-controlling-audio-volume-with-the-raspberry-pi/overview)
    - [Raspberry Pi Analog to Digital Converters](https://learn.adafruit.com/raspberry-pi-analog-to-digital-converters)
    - [MCP3008 - 8-Channel 10-Bit ADC With SPI Interface](https://learn.adafruit.com/mcp3008-spi-adc/python-circuitpython)
- SparkFun
    - [30A Non-Invasive Current Sensor](https://www.sparkfun.com/products/11005) [reviews](https://www.sparkfun.com/products/11005#reviews)
    - [Current Sensor Breakout (ACS723) Hookup Guide](https://learn.sparkfun.com/tutorials/current-sensor-breakout-acs723-hookup-guide)
- OpenEnergyMonitor
    - [CT sensors - An Introduction](https://docs.openenergymonitor.org/electricity-monitoring/ct-sensors/introduction.html)
    - [Current Transformer Installation](https://docs.openenergymonitor.org/electricity-monitoring/ct-sensors/installation.html)
    - [CT Sensors - Interfacing with an Arduino](https://docs.openenergymonitor.org/electricity-monitoring/ct-sensors/interface-with-arduino.html)
    - [How to build an Arduino energy monitor - measuring mains voltage and current](https://openenergymonitor.github.io/forum-archive/node/58.html)
