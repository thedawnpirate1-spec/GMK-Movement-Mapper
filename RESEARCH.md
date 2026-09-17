# Original GMK research and lessons applied

## What the supplied 1.0.5-alpha archive contains

The supplied `GMK-Driver-1.0.5-alpha.zip` is not an application source archive. It contains the public README, `.gitignore`, Inno Setup installer scripts and a ViGEm notice. The installer script packages an already-built `dist` folder, installs under Local AppData and explicitly supplies the application's working directory. The executable installer was inspected as an artifact and was not run.

The current official [gamingmodkits/GMK-Driver](https://github.com/gamingmodkits/GMK-Driver) default branch likewise exposes its installer directory and README, rather than the C# application implementation behind the 1.0.5-alpha binary. Its README confirms the ViGEm dependency and mentions a previous crash caused by an incorrect working directory.

An older public implementation, [TannerHollis/GMKDriverNet](https://github.com/TannerHollis/GMKDriverNet), explains the core architecture: GMK VID `0x0483`, joystick PIDs `0x5750` and `0x5751`, USB input through LibUsbDotNet, and virtual Xbox output through Nefarius ViGEm.

## The 1.0.5-alpha performance note

The supplied release screenshot describes three useful changes: disable ViGEm automatic report submission, submit one complete report for each changed state, skip unchanged mapped reports, and pause live HUD timers while the UI is inactive.

That issue did apply to the earlier Controller mode in this project. With `AutoSubmitReport = true`, setting two axes and several button states could cause several virtual-bus reports for one physical GMK sample. Version 17 now sets `AutoSubmitReport = false`, compares the complete quantized output with the last report, and calls `SubmitReport()` exactly once only after a change. Stopping movement sends one neutral report before disconnecting the virtual controller.

Keyboard mode did not have the problem: it already emits a key event only when the requested pressed/released state changes. Version 17 also pauses diagnostics and live drawing whenever the window is inactive or minimized. The USB reader, movement mapping, keyboard output, controller output and reconnect recovery remain active.

## Other lessons retained

- Direct USB ownership remains exclusive, so the original GMK program must be closed while this mapper is running.
- The virtual Xbox device exists only while Controller movement is started and is removed when movement stops or the app exits.
- Keyboard movement creates no virtual controller.
- HidHide remains a separately installed, signed system driver. The mapper configures its supported command-line interface rather than repackaging an unsigned copy.
- Profiles are normalized before use so corrupt or older settings cannot place deadzones, angles or calibration outside supported ranges.

No original 1.0.5-alpha application code was copied into this project.
