# DS4AudioUtility

[![License: MIT](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](LICENSE)
[![Target Framework](https://img.shields.io/badge/.NET-9.0%20%7C%209.0-purple.svg)](https://dotnet.microsoft.com/ru-ru/download/dotnet/9.0)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11-0078D6.svg)](https://microsoft.com)

## This utility allows you to transmit sound to the speaker or audio output of the DualShock 4 controller without a Sony USB dongle.


![Quick demo](/img/video.gif)


## Quick start 

* Download and install the latest version of [GStreamer](https://gstreamer.freedesktop.org/download)
* Download the [latest release](https://github.com/YurijChetverikov/DS4AudioUtility)
* Unzip it in any location on your PC
* Run the executable file

## Arguments

***Warning: you must write names of the arguments exactly in the case provided in the table below:***

| Argument name       | Description                                        | Default value                                                       |
| :---                | :---                                               | :---                                                                |
|--GStreamerPath      | Absolute path to the GStreamer executable          | `C:\Program Files\gstreamer\1.0\msvc_x86_64\bin\gst-launch-1.0.exe` |
|--DS4VId             | DS4 Vendor ID                                      | `1356` (`0x54C`)                                                    |
|--SaveDump           | Saves controller payloads to dump.txt              | `false`                                                             |
|--ReadBuffer         | Reads buffer every 10th payload                    | `false`                                                             |
|--ChannelMode        | SBC encoding: channel mode                         | `dual`                                                              |
|--Blocks             | SBC encoding: block count                          | `16`                                                                |
|--Subbands           | SBC encoding: subband count                        | `8`                                                                 |
|--Bitpool            | SBC encoding: bitpool count                        | `25`                                                                |
|--QueueSize          | DS4 payload queue size                             | `10`                                                                |
|--SpeakerVol         | Built-in speaker volume                            | `70`                                                                |
|--LeftEarVol         | Left ear volume                                    | `115`                                                               |
|--RightEarVol        | Right ear volume                                   | `115`                                                               |

### Dump

`SaveDump` enables a feature that saves every payload that has been sent to the controller to the `dump.txt` file in the application directory.

### SBC encoding

Two channel modes are implemented: `dual` and `joint`.

* For `dual`, we send a report with ID 0x17, which contains 448 bytes of audio data.
* For `joint`, we send a report with ID 0x18, which contains 460 bytes of audio data.

Calculating the possible encoder configuration depends on the channel mode you are using. Frequency is fixed at 32000 Hz.

To calculate a single audio frame size, you can use the formula:

* For `dual` channel mode:

```
FrameSize = 4 + (4 * subbands * channels / 8) + (blocks * channels * bitpool / 8)
``` 

* For `joint` channel mode:

```
FrameSize = 4 + (4 * subbands * channels / 8) + ((subbands + blocks * bitpool) / 8)
``` 

***You must set the encoder configuration so that your SBC frames fit perfectly into 448 (or 460) bytes of audio data.*** 

PlayStation uses this config:

* For `dual`

  * Blocks: 16
  * Subbands: 8
  * Bitpool: 53

* For `joint`

  * Blocks: 16
  * Subbands: 8
  * Bitpool: 51

### QueueSize

QueueSize sets the size of the controller payload queue.
10 is enough, but if you use enhanced audio quality, within a few minutes (or even seconds),
you'll start receiving `Queue is full. Frame was dropped` messages.

This indicates that the controller likely can't keep up with processing audio at this quality,
and increasing the queue size won't help.

### SpeakerVol, LeftEarVol and RightEarVol

From 0 to 255.

## Delay in DS4Windows

I used `dual` channel mode and default PS4 SBC encoder configuration. Poll inverval: 16ms. Average delay was 12-20ms

![DS4Windows screenshot](/img/delay.png)

## Algorithm

![Algorithm schema](/img/scheme.png)

## Known issues

### When connected to DS4Windows, it works incorrectly and disables the controller a second after it’s detected

I don't know why this happens. It is due to some collision between DS4Windows and my utility.
But we can fix this by occasionally reading the controller's buffer. 

I hardcoded it so that the program reads a report from the controller’s buffer every 10th payload it sends if `ReadBuffer` is `true`.

## Compilation

* Framework: .NET 9.0
* Dependencies: HidSharp 2.6.4



**Third-Party Licenses**

* [HidSharp](https://software.seekye.com/hidsharp) — Copyright 2010-2025 James F. Bellinger <http://software.seekye.com/hidsharp>