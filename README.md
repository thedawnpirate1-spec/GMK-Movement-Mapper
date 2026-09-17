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

## Installation

1. **Download** `GMKMovementMapper.exe` from the [Releases page](https://github.com/thedawnpirate1-spec/GMK-Movement-Mapper/releases). It is a single, self-contained Windows x64 executable — no installer, no separate .NET runtime, no Visual Studio.
2. **Put it anywhere** you like (Desktop, a Programs folder, wherever). Nothing needs to be extracted or installed alongside it.
3. **First launch — Windows SmartScreen warning is expected.** Because this is an independent, unsigned build, Windows shows *"Windows protected your PC"* the first time you run it. Click **More info**, then **Run anyway**. This is normal for small open-source tools without a paid code-signing certificate; it is not a sign of a virus, and you can verify that yourself by building from source (see below) or reading the code in `Source/`.
4. **(Optional) Install [HidHide](https://docs.nefarius.at/projects/HidHide/Simple-Setup-Guide/)** if you want the app to hide the physical GMK from games like Fortnite so only the mapped keyboard/controller input is seen. Not required for basic movement mapping to work.
5. Run the app and follow **First run** below.

### Building from source instead

Requires the [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0).

```bash
git clone https://github.com/thedawnpirate1-spec/GMK-Movement-Mapper.git
cd GMK-Movement-Mapper/Source/GMKMovementMapper
dotnet publish GMKMovementMapper.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

The finished executable is written to `bin\Release\net9.0-windows\win-x64\publish\GMKMovementMapper.exe`.

## First run

1. Close GMK DriverNet and AntiMicro.
2. Open GMK Movement Mapper and select Set up HidHide.
3. Select Keyboard or Controller movement and press Start movement before opening Fortnite.

HidHide remains a separately installed signed driver. This project does not redistribute or replace it.

## GMK not detected?

Some GMK hardware revisions don't expose the native USB interface this app looks for by default — see [XBOX_FALLBACK_GUIDE.md](XBOX_FALLBACK_GUIDE.md) for how to tell which revision you have and what to do if detection still fails after updating.

See RESEARCH.md for the original-driver findings and the performance changes applied in v17.
