import clr
import numpy as np
clr.AddReference('MicroControlLED-API')
from MicroControlLED_API.USB_Communication import SMILEUSBDevice

device = SMILEUSBDevice()
comport = device.GetComPorts();
#Connectionstatus = device.Connect(comport[0])
Connectionstatus = device.Connect("")
frame = [True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True,True]
print(comport[0])
print(Connectionstatus)
print(frame)
print(comport)

device.SendFramePython(frame)
device.Disconnect()