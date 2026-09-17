# Xbox-compatible fallback: what it is and how to use it

## The problem this solves

`GmkUsbSource` originally looked for exactly one USB identity: VID `0483`,
PID `5750` or `5751` — the STM32 chip the original GMK DriverNet app was built
around. Some GMK hardware revisions don't expose that native interface at
all. Windows still detects the device fine, but as a standard **Xbox 360
compatible gamepad** (VID `045E`, PID `028E`) instead. Since the mapper only
ever looked for `0483`, it always reported "GMK not connected", even with a
perfectly good cable, port, and driver setup.

You can tell which situation you're in:

- Open **Device Manager** while plugging the GMK in.
- If it appears as **"Xbox 360 Controller for Windows"**, you have this
  hardware revision. The fix below applies to you.
- If you can find it in [Zadig](https://zadig.akeo.ie) (Options → List All
  Devices) under VID `0483`, you have the original chip — nothing changes for
  you, the app already worked.

## What changed

1. **`GmkUsbSource`** now tries the native raw-USB interface first (unchanged
   behaviour). If that's not found, it automatically falls back to reading
   the device through **XInput** — the same API Windows uses for Xbox
   controllers — instead of failing outright.
2. **`XInputSource`** wraps `XInputGetState`. This needed extra care:
   - XInput devices do **not** show up through the legacy WinMM joystick API
     (`joyGetPosEx`), so that API can't be used for this fallback.
   - `XInputGetState` can hang for a long time — or indefinitely — against an
     unstable/flaky USB connection. Every query is single-flighted through one
     shared background task per controller slot (0-3), with a short bounded
     wait. A stuck slot can never spawn more than one abandoned thread for the
     whole lifetime of the app, no matter how many times "Start movement" or
     the automatic reconnect loop retries — and the UI thread never blocks
     longer than the bound.
   - The native raw-USB scan got the same treatment (single-flighted with a
     cooldown), because enumerating the USB bus can itself hang if *any*
     device on it — not necessarily the GMK — is flaky.
   - The per-frame read loop is throttled to ~250 Hz. XInput's `GetState` is a
     non-blocking "give me the state right now" call, unlike the native
     endpoint's blocking read which naturally paces itself to the device's
     own data rate. Without a throttle, the read loop spins as fast as the
     CPU allows and can peg multiple cores.
3. **"GMK controller slot" picker** (Setup & startup tab): if you also use a
   real Xbox/XInput controller at the same time as the GMK, automatic
   detection can't tell them apart — XInput doesn't expose a device's VID/PID
   per slot. Leave this on **Automatic** unless you notice the app reading
   input from the wrong device; then pick the specific slot the GMK is on
   (click **Refresh** to see live connection status per slot).

## If detection still fails after updating

This fallback only helps if the device shows up as an Xbox 360 compatible
gamepad. If Device Manager shows something else, or nothing at all when you
plug it in, that's a different problem — most likely the cable, the USB port,
or the connector inside the GMK module itself:

1. Try a different Micro-USB cable (even the original cable can develop a
   broken data line while still charging fine).
2. Try a different USB port, ideally directly on the motherboard I/O panel,
   not through a hub.
3. Use [Zadig](https://zadig.akeo.ie) → Options → **List All Devices**. If
   there's no entry at all for the GMK (neither `0483` nor `045E:028E`), the
   device isn't reaching Windows at the USB level — no software fix can help,
   only new hardware.

## Files touched

- `Source/GMKMovementMapper/Input/GmkUsbSource.cs`
- `Source/GMKMovementMapper/Input/XInputSource.cs` (new)
- `Source/GMKMovementMapper/Mapping/MovementProfile.cs` (new
  `ForcedControllerSlot` field)
- `Source/GMKMovementMapper/MainForm.cs` (new controller slot picker in
  Setup & startup)
