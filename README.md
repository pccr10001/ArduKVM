ArduKVM
---

### Introduction
* A simple KVM solution based on displays with DDC/CI.
* This program captures the keyboard and mouse inputs, then pass them to a STM32 board as keyboard and mouse.

### How To Use
* `Ctrl-Alt-Insert` is the hotkey to enable capture then switch display input.
* Find the input code for your display and replace it in code.

### Hardware
* A STM32F103C8C6 board with a native USB port, ex. BluePill
* libmaple and https://github.com/arpruss/USBComposite_stm32f1
* UART2(PA2, PA3) is running at baudrate 122880 to receive HID reports
  * Packet format `0x73 {KB REPORT}` for keyboard, `0x74 {MOUSE REPORT}` for mouse
 
### ToDo
* DFU firmware upgrade
* Running as system service to prevent losing control to external computers
* Migrate keyboard capturer to Interception

### References
* https://github.com/Ericvf/DDC-CI
* https://github.com/oblitum/Interception

