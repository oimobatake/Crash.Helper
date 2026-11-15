# Mask events

The UI calls `MaskService`; it never runs native commands on the WinForms thread.
The old pointer-write behavior remains in `Controls/DataControl.Masks.cs` and is
selected by `HelperSettings.LegacyMaskOption`. Xbox uses that path automatically.

## Files

- `MaskMemory.cs`: discover the local player's mask manager and event methods from
  engine-owned lists; read count, invincibility, death, loading and pause state.
- `MaskCommandCode.cs`: emit the immutable x64 game-thread dispatcher.
- `MaskCommandPatch.cs`: install/restore its hook and submit guarded commands.
- `MaskFreezePolicy.cs`: choose the next count for freeze and maskform-on-hit.
- `MaskService.cs`: own the process session, queue input, read state and reconnect.

Pause separates input availability (`CanConfigure`) from native execution
(`Ready`). Paused edits coalesce to a scene-bound final count, shown by
`DisplayCount`; undoing a third-mask selection therefore cannot start temporary
invincibility. The worker retains this selection and explicit freeze targets
until resume, then applies them without the automatic-correction throttle.
Loading, death, session/mode changes and shutdown discard incompatible selections.
The native pause guard remains zero, so no event executes inside the pause menu.
Cancellation reports an already-completed event so a pause cannot replay it.

## Game events

These names belong to the Steam game's `Scripts.Graph.Crash_AkuAkuManager` type;
product code and UI otherwise use Mask terminology.

| Operation | Engine entry |
| --- | --- |
| Collect, including the third mask | `GraphEvent_id_80tnqnrs_Definition` |
| Decrement | `New_Graph_Event_1_id_ibjgf1k9`, signed integer parameter at `+0x38` = -1 |
| Refresh model/state after decrement | `GraphEvent_id_9becl2sc_Definition` |
| End invincibility before decrement | `GraphEvent_id_xjwze7ot_Definition` |
| Ensure the mask model exists | `GraphEvent_id_a9nnfx8i_Definition` |
| Update the model's mask count | `New_Graph_Event_1_id_9nj5heau` |
| Apply/remove two-mask gold tint | `GraphEvent_id_5sryuo67_Definition` / `GraphEvent_id_202aweq4_Definition` |

Names have the prefix `Crash_AkuAkuManager_c.`. Decrement deliberately avoids the
natural damage event: damage immunity rejects repeated calls to that event.
Ending invincibility first stops the manager's
`_Internal_TimerAction_id_wwl6fvoo_timer` through the engine's timer-stop function
(`+0xCBC740`). This prevents its old expiry callback from removing another mask.

Automatic restoration uses signed delta +1, model spawn and model-count events
instead of collection/full refresh. Full refresh stops the post-hit timer and
clears `DamagedInvulnerable`, so it would erase the protection being preserved.
Restoration mirrors only the refresh graph's `currentState` bookkeeping field,
located by metadata; the gameplay count itself is still changed by the graph.
The model-count event alone does not restore the two-mask gold tint. Restoration
also invokes the game's tint-on/off event for the resulting count.
This also keeps later hit transitions correct if the helper is closed mid-hit.
Automatic downward corrections wait for post-hit immunity to end. Natural
third-mask invincibility is left alone; explicit target edits can still end it.

The invincibility timer is created lazily by the first third-mask pickup in each
scene. Its absence must not block discovery or ordinary mask commands. Its
pointer is guarded, so creation automatically refreshes the cached binding.

The signed-delta/collection graphs clamp against the assist floor in
`Int_id_tod2nk3r`. Each helper command guards and temporarily lowers that field to
zero on the game thread, then restores it before publishing success or failure.
This permits one-mask steps below a saved assist count without changing respawn
assistance. Guard rejection occurs before the temporary change.

Missing player pointers, empty player lists and other normal spawn transitions
raise `MaskNotReadyException`, which discovery converts to an unavailable snapshot
without an error log. Signature/type mismatches and unexpected failures still log.

## Dispatcher lifetime

The verified Steam build is 30,883,840 bytes. The five-byte instruction at
`module+0x4E3830` is replaced with a near jump while game threads are suspended.
Native function signatures are checked before installation. No debugger or new
game thread is used. Only the earliest existing game thread claims commands.

The dispatcher preserves flags, volatile integer registers and XMM0–XMM5, with
aligned stack/shadow space. It constructs a real engine message, retains it in a
variant, invokes the graph, then releases both references. All mask count changes
are performed by the game's graph; the helper does not write the count directly.

Its 8 KiB allocation contains an immutable executable page and a writable mailbox.
Mailbox offsets: pending/status 0/4, action/guard count 8/12, owner 16, primary
method/meta 24/32, refresh method/meta 48/56, end method/meta 64/72, end flag 80,
timer 88, spawn method/meta 96/104, model method/meta 112/120, runtime-state/count
addresses 128/136, guard records `0x100`, message `0x800`, variant `0x820`.
Assist-floor address/saved value are at 144/152; tint-on method/meta at 160/168;
tint-off method/meta at 176/184. Up to 72 guard records fit before the message.
Actions 0/1/2 mean collect/remove/restore; signed delta is at 40.
Each guard record is address/value/size in 24 bytes. Sizes 1/4/8 compare equality;
-4 checks a minimum component count, since visual effects change the list's tail.
The manager slot, array, actor, level, variable storage and expected state remain
guarded. Guard rejection retries manual input only within its original scene.

Mode changes, helper disable, update suspension and shutdown cancel unclaimed
commands and restore the hook. An active command is allowed to finish first.
The small allocation stays until game exit so a thread already returning through
the dispatcher cannot execute freed memory.
