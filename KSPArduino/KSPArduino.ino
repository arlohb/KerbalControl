#include <Wire.h>
#include <LiquidCrystal_I2C.h>

// SDA goes to A4 on the Uno and SCL to A5
LiquidCrystal_I2C lcd(0x27,20,4);

String padLeft(String string, String paddingChar, int len){
  String padding = "";
  
  for(int i=0; i<(len-string.length()); i++){
    padding += paddingChar;
  }
  
  return padding + string;
}

class RotarySwitch{
  public:
    int value;
    RotarySwitch(int _pin, int _ways, int _waitTime){
      pin = _pin;
      ways = _ways;
      waitTime = _waitTime;
      
      pinMode(pin, INPUT);
      last = -1;
      value = -1;
      changeTime = 0;
    }
  int pin;
  int ways;
  int last;
  int changeTime;
  
  int waitTime;
  
  void Update(){
    // get the current position ( or floating )
    int temp = round((float)analogRead(pin)*((float)(ways-1)/1023.0));
    
    // if it's different
    if(temp != last){
      // update last pos
      last = temp;
      // update change time
      changeTime = millis();
    }

    // if proper value isn't current value
    if(value != temp){
      // if it's been 400ms
      if(millis()-changeTime >= waitTime){
        // change proper value
        value = temp;
        // update change time
        changeTime = millis();
      }
    }
  }
};

// == Prefixes == //
// l = last
// v = value
// c = change time, using millis()

int potUL = A11;
int potUR = A10;
int potBL = A12;
int potBR = A13;

int btnUL = 24;
int btnUR = 25;
int btnBL = 23;
int btnBR = 22;

int switch1 = 2;
int switch2 = 3;
int switch3 = 4;
int switch4 = 5;

int potLCD = A14;
int btnKey = A3;
int switchKey = A2;

RotarySwitch rot6 = RotarySwitch(A15, 6, 200);
RotarySwitch rot12 = RotarySwitch(A0, 12, 1200);

// {potUL, potUR, potBL, potBR, btnUL, btnUR, btnBL, btnBR, switch1, switch2, switch3, switch4, rot6, potLCD, rot12, btnKey, switchKey};
int inputs[17] = {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, -1, 0, -1, 0, 0};

char terminator = 0b00001010;


void setup() 
{
  lcd.init();
  //lcd.backlight();

  Serial.begin(115200);

  pinMode(potUL, INPUT);
  pinMode(potUR, INPUT);
  pinMode(potBL, INPUT);
  pinMode(potBR, INPUT);

  pinMode(btnUL, INPUT_PULLUP);
  pinMode(btnUR, INPUT_PULLUP);
  pinMode(btnBL, INPUT_PULLUP);
  pinMode(btnBR, INPUT_PULLUP);

  //pinMode(switch1, INPUT_PULLUP);
  pinMode(switch2, INPUT_PULLUP);
  pinMode(switch3, INPUT_PULLUP);
  pinMode(switch4, INPUT_PULLUP);

  pinMode(potLCD, INPUT);
  pinMode(btnKey, INPUT_PULLUP);
  pinMode(switchKey, INPUT_PULLUP);
}


void loop()
{
  delay(50);
  
  rot6.Update();
  rot12.Update();
  
  // {potUL, potUR, potBL, potBR, btnUL, btnUR, btnBL, btnBR, switch1, switch2, switch3, switch4, rot6, potLCD, rot12, btnKey, switchKey};
  int potULvalue = analogRead(potUL); // random ?
  int potURvalue = analogRead(potUR); // nothing
  int potBLvalue = analogRead(potBL); // nothing
  int potBRvalue = 1023 - analogRead(potBR);

  // [0] 1, 1, 1, 3*switches, key, keyBtn
  // [1] 1, 3 bits for rot6, 4 bits for rot12
  // [2] 1, 4*buttons, 3 bits for lcdPot
  byte serial[] = {0b11100000, 0b10000000, 0b10000000};

  // 000x 0000
  serial[0] = serial[0] | (!digitalRead(switch2) * 16);
  // 0000 x000
  serial[0] = serial[0] | (!digitalRead(switch3) * 8);
  // 0000 0x00
  serial[0] = serial[0] | (!digitalRead(switch4) * 4);
  // 0000 00x0
  serial[0] = serial[0] | (!digitalRead(switchKey) * 2);
  // 0000 000x
  serial[0] = serial[0] | (!digitalRead(btnKey) * 1);

  // bitRead starts from least-significant, right-most
  // 0x00 0000
  serial[1] = serial[1] | (bitRead(rot6.value, 2) * 64);
  // 00x0 0000
  serial[1] = serial[1] | (bitRead(rot6.value, 1) * 32);
  // 000x 0000
  serial[1] = serial[1] | (bitRead(rot6.value, 0) * 16);
  // 0000 x000
  serial[1] = serial[1] | (bitRead(rot12.value, 3) * 8);
  // 0000 0x00
  serial[1] = serial[1] | (bitRead(rot12.value, 2) * 4);
  // 0000 00x0
  serial[1] = serial[1] | (bitRead(rot12.value, 1) * 2);
  // 0000 000x
  serial[1] = serial[1] | (bitRead(rot12.value, 0) * 1);

  // 0x00 0000
  serial[2] = serial[2] | (!digitalRead(btnUL) * 64);
  // 00x0 0000
  serial[2] = serial[2] | (!digitalRead(btnUR) * 32);
  // 000x 0000
  serial[2] = serial[2] | (!digitalRead(btnBL) * 16);
  // 0000 x000
  serial[2] = serial[2] | (!digitalRead(btnBR) * 8);
  // 0000 0xxx
  byte lcdRot = analogRead(potLCD) * (7.0/1023.0);
  serial[2] = serial[2] | (bitRead(lcdRot, 2) * 4);
  serial[2] = serial[2] | (bitRead(lcdRot, 1) * 2);
  serial[2] = serial[2] | (bitRead(lcdRot, 0) * 1);

  Serial.write(serial[0]);
  Serial.write(serial[1]);
  Serial.write(serial[2]);
  Serial.write(0b00001010);

}
