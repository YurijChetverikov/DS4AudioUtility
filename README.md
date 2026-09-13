# DS4AudioUtility

## This utility allows you to transmit sound to the speaker or audio output of the DualShock 4 controller without a Sony USB dongle.

## Quick start 

* Download [Latest release](https://github.com/YurijChetverikov/DS4AudioUtility)
* Unzip it in any location on your PC
* Run executable file

## Arguements

Warning: you must write names of the arguments exactly in the case provided in the table below:

| Arguement name      |  Description  									|  Default value                                                             								      |
|      :---           |     :---      									|        :---                                                                    								  |
|--GStreamerPath      |Absolute path to GStreamer executable    		|`C:\Program Files\gstreamer\1.0\msvc_x86_64\bin\gst-launch-1.0.exe`                   							  |
|--BufferReadSize     |Bytes to read from controller    				|	`512`				                                                         					  |     
|--Frequency          |Audio sampling rate							    |	`32000`				                                                         					  |     
|--Blocks             |SBC Header: blocks count 						|	`16`			                                                         					  |     
|--Subbands           |SBC Header: subbands count						|	`8`			                                                         					  |     
|--Bitpool            |SBC Header: bitpool count						|	`53`		                                                         					  |     
|--QueueSize          |Audio frame queue size 					        |	`10`				                                                         					  |     
|--SpeakerVol         |Builtin speaker volume 					        |	`70`				                                                         					  |     
|--LeftEarVol         |Left ear volume 					        		|	`115`				                                                         					  |     
|--RightEarVol        |Right ear volume 					        	|	`115`				                                                         					  |     


## Usage

Executable file accepts arguements:



## Compilation

Framework: .NET 9.0
Dependencies: HidSharp 2.6.4

## 