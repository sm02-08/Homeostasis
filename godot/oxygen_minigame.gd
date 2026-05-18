extends Node2D

# ─────────────────────────────────────────────
#  OXYGEN MINIGAME
#  Listens to CentralHardwareHub (Autoload) for
#  physical button presses. Player must press the
#  arcade button to keep the oxygen bar full.
# ─────────────────────────────────────────────


# ── CONSTANTS ──────────────────────────────────────────────────────────────────

# The oxygen bar is "full" at 30 press-units.
# This is both the max value for the ProgressBar and the starting value.
const MAX_OXYGEN := 30.0

# How many press-units drain away every second when the player does nothing.
# At 2.0/sec, a full bar empties in exactly 15 seconds of no input.
const DRAIN_RATE := 2.0

# Each physical button press adds this many press-units instantly.
# Set to 1.0 so one press = one unit. Tune upward to make it easier.
const PRESS_REFILL := 1.0


# ── STATE VARIABLES ────────────────────────────────────────────────────────────

# Current oxygen level. Starts full.
# This is a float so drain math stays precise (no integer rounding per frame).
var oxygen := MAX_OXYGEN

# When true, the game loop (drain + refill) is active.
# Set to false on game over so _process stops doing anything.
var game_active := true


# ── NODE REFERENCES ────────────────────────────────────────────────────────────
# @onready fetches these once when the scene enters the tree.
# The string paths match the node names you created in Step 2.
# If you rename a node, update the matching string here.

@onready var oxygen_bar: ProgressBar = $OxygenBar
@onready var status_label: Label     = $StatusLabel
@onready var press_count_label: Label = $PressCountLabel


# ── _READY ─────────────────────────────────────────────────────────────────────
func _ready() -> void:
	# Configure the ProgressBar's range to match our constant.
	# We do this in code (not just the editor) so the bar always matches MAX_OXYGEN
	# even if you change the constant later — one source of truth.
	oxygen_bar.min_value = 0
	oxygen_bar.max_value = MAX_OXYGEN
	oxygen_bar.value     = MAX_OXYGEN

	# ── CONNECT TO THE HUB ───────────────────────────────────────────────────
	# CentralHardwareHub is an Autoload, meaning Godot registers it as a global
	# singleton accessible by name from anywhere in the project.
	# We grab it here and connect its ButtonPressed signal to our handler function.
	#
	# Why connect here instead of in the editor?
	# Because the hub is an Autoload (not a child of this scene), you can't drag-connect
	# it in the editor. Code connection is the right approach for Autoload signals.
	var hub = get_node("/root/CentralHardwareHub")

	# .connect() wires the signal to a Callable — a reference to a function.
	# When the hub emits ButtonPressed, _on_button_pressed() runs immediately.
	hub.connect("ButtonPressed", _on_button_pressed)

	# Update the UI labels to reflect the starting state.
	_update_ui()


# ── _PROCESS ───────────────────────────────────────────────────────────────────
# _process runs every frame (roughly 60 times/sec on a standard display).
# delta is the time in SECONDS since the last frame (~0.016 at 60fps).
# We use delta so drain speed is frame-rate independent — it always drains
# at exactly DRAIN_RATE units per second, regardless of FPS.

func _process(delta: float) -> void:
	# If the game is over, do nothing. The scene stays visible but frozen.
	if not game_active:
		return

	# ── DRAIN ────────────────────────────────────────────────────────────────
	# Subtract a small slice of DRAIN_RATE each frame.
	# Example: at 60fps, delta ≈ 0.0167. DRAIN_RATE * 0.0167 ≈ 0.033 per frame.
	# Over 60 frames (1 second), that totals exactly 2.0 units drained.
	oxygen -= DRAIN_RATE * delta

	# ── CLAMP ────────────────────────────────────────────────────────────────
	# clamp() prevents oxygen from going below 0 or above MAX_OXYGEN.
	# Without this, oxygen could go negative, which would break the bar visually
	# and cause the game-over check to fire multiple times.
	oxygen = clamp(oxygen, 0.0, MAX_OXYGEN)

	# ── GAME OVER CHECK ──────────────────────────────────────────────────────
	# Check AFTER clamping so we only trigger once, exactly at 0.
	if oxygen <= 0.0:
		_game_over()
		return  # Skip UI update this frame — _game_over handles its own label.

	# ── REFRESH UI ───────────────────────────────────────────────────────────
	_update_ui()


# ── SIGNAL HANDLER ─────────────────────────────────────────────────────────────
# This function is called by the hub's ButtonPressed signal.
# It runs OUTSIDE of _process — it fires the instant the hub detects
# a press edge, which could be between frames. Godot queues it safely.

func _on_button_pressed() -> void:
	# Ignore presses after game over — the hub keeps running in the background.
	if not game_active:
		return

	# Add one press-unit of oxygen instantly.
	# clamp() keeps it from exceeding the maximum (no overflow exploits).
	oxygen = clamp(oxygen + PRESS_REFILL, 0.0, MAX_OXYGEN)

	# UI update is called here too so the bar visually jumps up immediately
	# on the frame the press is detected, not on the next _process tick.
	_update_ui()


# ── UI UPDATE ──────────────────────────────────────────────────────────────────
# Centralised function that syncs all visual elements to the current game state.
# Called from both _process (continuous drain updates) and _on_button_pressed
# (instant press feedback). One function = no duplicate label-setting code.

func _update_ui() -> void:
	# Sync the ProgressBar's fill to the current oxygen float.
	oxygen_bar.value = oxygen

	# Show the oxygen level as a rounded integer / max for readability.
	# roundi() rounds to the nearest int so "29.97" shows as "30", not "29".
	press_count_label.text = "%d / %d" % [roundi(oxygen), int(MAX_OXYGEN)]

	# Colour-code the status label based on how critical oxygen is.
	# This gives the player an at-a-glance urgency signal without needing to read numbers.
	if oxygen > MAX_OXYGEN * 0.5:
		# Above 50% — safe zone. Calm white text.
		status_label.text = "Keep pressing!"
		status_label.modulate = Color.WHITE
	elif oxygen > MAX_OXYGEN * 0.25:
		# 25–50% — caution zone. Orange warning.
		status_label.text = "PRESS FASTER!"
		status_label.modulate = Color(1.0, 0.5, 0.0)  # Orange
	else:
		# Below 25% — danger zone. Red urgent warning.
		status_label.text = "RUNNING OUT!"
		status_label.modulate = Color.RED


# ── GAME OVER ──────────────────────────────────────────────────────────────────
func _game_over() -> void:
	# Halt the game loop. _process will return early from now on.
	game_active = false

	# Snap the bar to zero so it looks empty, not nearly-empty.
	oxygen_bar.value = 0
	press_count_label.text = "0 / %d" % int(MAX_OXYGEN)

	# Update the status label to reflect the end state.
	status_label.text = "YOU BLACKED OUT."
	status_label.modulate = Color.RED
