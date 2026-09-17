# GMK Movement Mapper

A clean-room, low-latency Windows mapper for GMK joysticks. It supports eight-way keyboard movement and an optional on-demand virtual Xbox controller with fully configurable forward diagonal locking.

## Highlights

- Direct GMK USB input for VID 0483 and joystick PIDs 5750/5751
- Custom movement keys and GMK A-button output
- Keyboard mode creates no virtual controller
- Controller mode exists only while movement is running
- Configurable diagonal target, range, rotation, deadzones and response curve
- HidHide setup, profiles, calibration, diagnostics and reconnect recovery
- DPI-aware interface, tray performance mode and Dark/Light/Black themes

## Version 17 performance changes

ViGEm automatic report submission is disabled. Controller mode submits one complete report only when its quantized axes or mapped button state changes. Live graphics and diagnostics pause while the app is inactive or minimized; input and output continue at full speed.

## Download

Download the self-contained Windows x64 executable from the Releases page. Visual Studio and a separate .NET installation are not required.

## First run

1. Close GMK DriverNet and AntiMicro.
2. Open GMK Movement Mapper and select Set up HidHide.
3. Select Keyboard or Controller movement and press Start movement before opening Fortnite.

HidHide remains a separately installed signed driver. This project does not redistribute or replace it.

See RESEARCH.md for the original-driver findings and the performance changes applied in v17.
