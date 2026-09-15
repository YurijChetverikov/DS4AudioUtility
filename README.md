# DS4AudioUtility

## This utility allows you to transmit sound to the speaker or audio output of the DualShock 4 controller without a Sony USB dongle.

## Quick start 

* Download and install latest verison of [GStreamer](https://gstreamer.freedesktop.org/download)
* Download [Latest release](https://github.com/YurijChetverikov/DS4AudioUtility)
* Unzip it in any location on your PC
* Run executable file

## Arguements

***Warning: you must write names of the arguments exactly in the case provided in the table below:***

| Arguement name      |  Description  									|  Default value                                                             								      |
|      :---           |     :---      									|        :---                                                                    								  |
|--GStreamerPath      |Absolute path to GStreamer executable    		|`C:\Program Files\gstreamer\1.0\msvc_x86_64\bin\gst-launch-1.0.exe`                   							  |
|--BufferReadSize     |Bytes to read from controller    				|	`512`				                                                         					  |     
|--Frequency          |Audio sampling rate							    |	`32000`				                                                         					  |     
|--Blocks             |SBC encoding: blocks count 						|	`16`			                                                         					  |     
|--Subbands           |SBC encoding: subbands count						|	`8`			                                                         					  |     
|--Bitpool            |SBC encoding: bitpool count						|	`25`		                                                         					  |     
|--QueueSize          |DS4 payloads queue size 					        |	`10`				                                                         					  |     
|--SpeakerVol         |Builtin speaker volume 					        |	`70`				                                                         					  |     
|--LeftEarVol         |Left ear volume 					        		|	`115`				                                                         					  |     
|--RightEarVol        |Right ear volume 					        	|	`115`				                                                         					  |     

### BufferReadSize

We need sometimes to read the controller's buffer so that DS4Windows works correctly and doesn’t disable the controller a second after it’s detected. 

So BufferReadSize can be `256`, `512`, `1024` or another value - you can adjust this value for your setup for better experience.

### SBC encoding

Because we using report with id 0x17 which contains 448 bytes of audio data, we need to make sure size of our SBC frame <= 448 bytes.

To calculate single audio frame size, you can use formula:

```
FrameSize = 4 + (4*subbands*channels/8) + (blocks*channels*bitpool/8)
``` 
Below, I have listed the possible SBC encoder configuration options:

|SBC frame size|Frames in payload|Blocks|Subbands|Bitpool|
|:---|:---|:---|:---|:---|
28|16|4|4|20
32|14|4|4|24
32|14|4|8|20
56|8|4|8|44
56|8|4|4|48
56|8|8|4|24
56|8|8|8|22
64|7|4|4|56
64|7|4|8|52
64|7|8|4|28
64|7|8|8|26
112|4|8|4|52
112|4|8|8|50
112|4|16|4|26
112|4|16|8|25
224|2|12|4|72
224|2|16|4|54
224|2|16|8|53



PlayStation uses this config:

* Blocks: 16
* Subbands: 8
* Bitpool: 25
* Frequency: 32000

I managed to get it to work well at slightly higher configuration:

* Blocks: 16
* Subbands: 8
* Bitpool: 53
* Frequency: 32000

### QueueSize

QueueSize sets the size of the controller payloads queue.
10 is enough, but if you use enhanced audio quality, within a few minutes (or even seconds),
you'll start receiving `Queue is full. Frame was dropped` messages.

This indicates that the controller likely can't keep up with processing audio at this quality,
and increasing the queue size won't help.

### SpeakerVol, LeftEarVol and RightEarVol

From 0 to 255.

## Knows issues

### When connected to DS4Windows, it works incorrectly and disables the controller a second after it’s detected

I dont know why this happens. Some collision between DS4Windows and my utility.
But we can fix this by sometimes read the controller's buffer. 

I hardcoded it so that the program reads from the controller’s buffer `BufferReadSize` once before starting.

## Compilation

* Framework: .NET 9.0
* Dependencies: HidSharp 2.6.4



**Third-Party Licenses**

* [HidSharp](https://software.seekye.com/hidsharp) — Copyright 2010-2025 James F. Bellinger <http://software.seekye.com/hidsharp>