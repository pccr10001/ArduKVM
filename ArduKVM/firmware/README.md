ArduKVM Hardware
---

### Introduction
A simple devices based on STM32 to emulate the keyboard and mouse.

### Protocol
* `0x73` for keyboard reports (8 bytes).
* `0x74` for mouse reports (4 bytes).

### References
* https://github.com/arpruss/USBComposite_stm32f1
  * add `setReport` to send HID reports directly.
