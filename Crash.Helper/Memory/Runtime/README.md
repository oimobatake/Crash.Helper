# Engine transforms (Steam)

`RuntimeMemory` resolves the local `CPlayer` through `CPlayerManager`, then follows its current hero handle. It selects an initialized, zone-loaded, enabled `CCameraSystemInstance` owned by that player. Some single-player levels leave the owner handle empty; an unowned instance is accepted only when exactly one eligible unowned camera exists. Foreign owners and ambiguous instances are rejected. Camera reads do not install a hook or scan for a recently copied model.

`RuntimeCode` is the small AMD64 emitter shared by the player dispatcher, position-store filters and final camera commit. Mask events keep their independent implementation.

## Player position

- Display and input use the hero's world-space entity origin in the engine's original units, with no coordinate conversion. The previous physics-capsule center used meters, was about 68.9 engine units above this origin during ordinary walking, and could stop updating while riding.
- Unattached actors can be read directly as one XYZ snapshot. Attached actors are sampled through the engine's world-position getter, including the parent/bone transform.
- `PlayerPositionPatch` submits axis-specific world targets, never writes a saved physics address, and executes the actor's own teleport on the game's original main thread. Unedited axes come from a fresh native world-position read.
- `PositionWriteFilter` suppresses the selected axes at the local and world entity-position stores. Other axes and other actors retain their game updates. Helper teleports bypass the filter only during the main-thread dispatcher command. The movement owner's unattached root is filtered; attached heroes retain their local animation, while guarded teleports maintain the requested world position and physics synchronization.
- Built-in vehicle handles and active `Crash_Ride_*` scripts with named ride handles identify the movement owner. Scripted rides are selected only after the walking physics controller is disabled, rather than when a nearby vehicle is prepared. Empty weak handles after dismount are ignored. Jetboard, bike and bear scripts use this route. The vehicle receives the same world delta as the hero, keeping their rider offset intact.
- Ride references include plain `RideEntity`, generated `RideEntity_*` fields and named `Ride*Entity` fields. Enabled, bolted `CVehicleRiderComponent` handles provide another engine-owned route when the player's vehicle handle is temporarily empty. Candidate actors still require their lifecycle and teleport-method guards. Swimming keeps its own actor; its SwimJet is an attached tool rather than an independent actor to teleport.
- Pointer ownership, list storage, 32-bit actor identity (without adjacent movement-state fields), spawn/enabled flags, the death state and the current level are checked again immediately before a native command. If any guard changes, the command is discarded before invoking the game.
- Missing or ambiguous objects return unavailable. Coordinate controls show `-` and reject writes. A full fade, or disabling the fade writer, does not by itself mean the actor is missing.
- Loading cancels queued edits and clears position freezes. A replacement actor or a walking/riding switch cancels frozen axes and held adjustments instead of replaying the previous movement owner's coordinates. The short handover state is readable but rejects edits until a movement owner is available. Movement time accumulates while a native request waits for the next game frame.
- A pooled actor can keep its address and ID across levels. Scene and attachment identity invalidate old snapshots, and the worker always adopts the newest validated ownership guards. Display reads use a separate lightweight actor binding instead of depending on successful vehicle/physics discovery.

## Camera

`OwnedCameraPatch` reads the selected instance's embedded camera model. A conditional hook is installed only when position or rotation control is enabled. It commits the controlled pose after the game's zoom, shake and override passes, immediately before the final camera transform is consumed. Only enabled Control groups receive targets; other groups and the roll retain normal game updates. Other instances remain untouched.

Commands use three immutable slots and atomic active/in-use pointers. The game claims a complete packet and the host never overwrites an active or claimed packet. Motion does not suspend the game each tick. A sequence-checked automatic-pose snapshot supplies a fresh baseline after a scene or player change. During the pause menu, manual edits also update the live model while the game threads are briefly suspended and ownership is rechecked. That pose is retained for the first resumed update.

Orbit advances continuous angle and radius state, rather than feeding a delayed rendered camera back into its next update. Position and direction are derived from one spherical pose. The native commit adds its orbital offset to the player's world position sampled in that same game frame, including vertical motion. Follow's vertical deadzone does not apply to Orbit. Manual edits and camera TP recapture the orbit.

Ordinary Follow also runs in the native commit. It retains XYZ smoothing state and tracked height by scene epoch and follow generation, reads the same frame's actor position and uses the guarded `CClock._gameFrameTime`. XMM6 at the late camera hook is an absolute-value mask, not the delta time it held near the function entry. Follow's blend uses a positive reciprocal polynomial approximation to the exponential approach; rates remain stable across 30/60/120 Hz. Immutable host packets supply only offset, deadzone, rate and generation. Manual edits recapture the generation, while ordinary packet replacement retains native smoothing state.

Missing players suspend control and release the death/respawn camera to the game. Death also clears and disables the Control, Follow and Orbit checkboxes and held inputs. Respawn restores availability without re-enabling control. Camera selection first considers containing activation volumes, then the distance to complete world-space spline paths, with activation-volume distance as a fallback for other camera types. Activation boxes can mark entrances far from the area actually viewed. Candidates include configured cameras, active overrides and camera blends, including nested boxes. Spline points come from engine-owned spline components; uncached parent-space points use the live entity quaternion and scales, avoiding a dirty transform matrix cache. Parent entities are followed through ordinary CEntityBolt links; an empty target contributes identity, matching the engine. Named bone attachments require a valid engine world-spline cache. The inspected quaternion convention is conjugated when transforming points. Segment distance approximates the path between its authored control points. For paths in the same selection class with distance within five percent of the nearest path, the current native camera wins. This retains authored offsets at overlapping rails and entrance boundaries; a distinctly nearer bonus or alternate path still replaces it.

Return uses the engine's camera setter to install the selected camera in its owning box, followed by the complete gameplay-camera reset. That resets the activation chain, spline state, targeting, zoom, modified-camera list, update caches and final model. A temporary default box is excluded because the native reset replaces it. This selection has no per-stage names or addresses.

A return requested while paused takes effect on resuming. Each return has a monotonically increasing request ID retained in subsequent motion packets until native completion acknowledges it. Repeated packets cannot replay a completed return; a temporarily invalid binding leaves the request pending instead of silently losing it. The next acknowledged publication removes reset-only guards, including the replaced default-box pointer, so any remaining Control group stays available.

Position TP exposes native completion IDs. The optional camera reset runs only after a successful teleport with both camera Control groups off. Active Control, Follow and Orbit settings remain unchanged by Position TP. Teleport completion is cancelled on loading, actor/driver replacement, a superseding edit or shutdown. Held TP requests are coalesced for camera resets. The Settings option defaults on and is independent of shortcut assignment.

Both transforms use engine units. Follow Scale is removed. XYZ speeds default to 30 and the Follow vertical deadzone to 60; prior unmodified defaults upgrade while customized values remain intact. Xbox applies fixed unit compensation at the service boundary so its existing coordinate path retains the same behavior. The camera retains its Control XYZ and Control YawPitch interface.

## Inspected entry points

| Steam RVA | Purpose |
| --- | --- |
| `0x1A665E8` | Current player manager |
| `0x1A890E0` | Camera system |
| `0xC003B0` | Entity world-position getter / guarded player command dispatch |
| `0x508EA0` | Actor teleport with component and physics notifications |
| `0xC0582A`, `0xC05B6D` | Local/world entity-position stores, selectively suppressed by Freeze |
| `0x377B00` | Final camera commit after shake, zoom and override passes |
| `0x36DA30` | Engine setter for a camera box's active camera |
| `0x36B7B0` | Complete gameplay-camera reset on the selected instance |
| `0x1A61888` | CClock singleton; game-frame time at `+0x28` |

Native code signatures and the recognized module size are required before installing either patch. No debugger or remote thread is used. Original instructions are restored on shutdown; published code pages stay allocated until game exit so an in-flight epilogue remains valid.

Xbox PC retains its existing position and camera path, including the conservative fade restrictions. These Steam offsets must not be applied to that build.

## Validation

Native fixtures cover partial-axis teleports, equal rider/vehicle displacement, invalid ownership chains, selective stores, helper-only bypass, concurrent store filters, same-frame Follow/Orbit, Follow deadzone and game-clock smoothing, death-camera release, nearest-camera return and concurrent immutable packet publication. Geometry checks cover moving pivots, steep pitches, repeated turns and angle wraparound. Live paused movement checks cover Hog Wild, Orient Express, Bye Bye Blimps, Under Pressure with and without SwimJet, Makin' Waves and Hog Ride, alongside the earlier Air Crash and Bear It checks. Spaced Out verifies camera-path selection outside the normal platform entrance and bonus paths whose entity bolt has no target. These checks establish the shared routes; they do not constitute a gameplay pass through every special-movement stage.
