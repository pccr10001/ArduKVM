#include <Arduino.h>
#include <USBComposite.h>

USBHID HID;
HIDKeyboard Keyboard(HID);
HIDMouse Mouse(HID); 

uint8_t buffer[16];
int len = 0;

void setup() {
  pinMode(PC13, OUTPUT);
  digitalWrite(PC13, LOW);

  Serial1.begin(115200);
  Serial2.begin(1228800);
  
  Serial1.println("Hello from STM32F103C8T6");
  HID.begin(HID_KEYBOARD_MOUSE);

  while(!USBComposite);
  Serial1.println("USB initialized");
  digitalWrite(PC13, HIGH);
}

void loop() {
  
  while(Serial2.available()) {
    uint8_t tmp = Serial2.read();
    if(tmp == 0x73 || tmp == 0x74){
      len = 0;
    }
    buffer[len++] = tmp;
    if(len == 16){
      len=0;
    }else if(buffer[0] == 0x73 && len == 9){
      break;
    }else if(buffer[0] == 0x74 && len == 5){
      break;
    }
  }
  char type = buffer[0];
    
    if(type == 0x73) {
      Keyboard.setReport(buffer+1);
    } else if (type==0x74){
      Mouse.setReport(buffer+1);
    }
    len=0;
}
