extends Node2D

# ─────────────────────────────────────────────────────────────────────────────
#  THERMAL MINIGAME
#
#  Every 10 seconds, the player is challenged to warm the DHT11 sensor by
#  rubbing it. The sensor only needs to rise 0.5°F in reality — but the UI
#  shows this mapped onto a 0→30°F fake scale so the player thinks they're
#  doing much more work.
#
#  Subscribes to: CentralHardwareHub.TelemetryUpdated (Autoload signal)
#  Reads: temperature field (data[1] from the serial packet)
# ─────────────────────────────────────────────────────────────────────────────


# ── CONSTANTS ─────────────────────────────────────────────────────────────────

# How many seconds the player has to warm the sensor each round.
const CHALLENGE_DURATION := 10.0

# The REAL temperature rise needed to win, in °F.
# The DHT11 only needs to climb this much. Keep this private knowledge.
const REAL_THRESHOLD := 0.5

# The FAKE temperature rise shown to the player on screen.
# The bar and number display treat this as the target.
# Changing this number only affects visuals — the real threshold stays 0.5.
const FAKE_TARGET := 30.0

# How many seconds of idle time between rounds before the next challenge fires.
const IDLE_DURATION := 10.0


# ── STATE MACHINE ─────────────────────────────────────────────────────────────
# The game moves through three distinct states. Using an enum makes the code
# readable — instead of checking magic numbers like "if state == 2", you check
# "if state == State.CHALLENGE". 

enum State {
	IDLE,       # Waiting between rounds. Timer counts down to next challenge.
	CHALLENGE,  # Active round. Player must heat the sensor before time runs out.
	RESULT      # Win or lose screen is showing. Waits briefly then resets.
}

var state: State = State.IDLE

# Counts down in _process using delta. Used for both the idle wait and the
# challenge countdown. Reused for the result display pause too.
var timer := IDLE_DURATION

# The temperature reading at the exact moment a challenge round starts.
# We compare every incoming reading against this baseline to measure the rise.
# Without snapshotting the baseline, we'd have no reference point to measure from.
var baseline_temp := 0.0

# The most recent temperature received from the hub signal.
# Updated every time TelemetryUpdated fires — which is ~33 times/sec.
var current_temp := 0.0

# Tracks whether we've received at least one valid temperature reading.
# Prevents a challenge from starting before the sensor has reported anything.
var has_first_reading := false


# ── NODE REFERENCES ───────────────────────────────────────────────────────────

@onready var challenge_label:   Label       = $ChallengeLabel
@onready var timer_label:       Label       = $TimerLabel
@onready var thermal_bar:       ProgressBar = $ThermalBar
@onready var temp_display_label: Label      = $TempDisplayLabel


# ── _READY ────────────────────────────────────────────────────────────────────

func _ready() -> void:
	# Set the bar's range to the FAKE scale the player will see.
	# max_value = 30 means the bar looks full at "30 degrees gained".
	thermal_bar.min_value = 0.0
	thermal_bar.max_value = FAKE_TARGET
	thermal_bar.value     = 0.0

	# Connect to the Autoload hub.
	# TelemetryUpdated carries: motionStatus, temperature, joyX, joyY, buttonState
	# We bind it to our handler which reads only the temperature argument.
	var hub = get_node("/root/CentralHardwareHub")
	hub.connect("TelemetryUpdated", _on_telemetry_updated)

	# Start in idle state — show the player something while waiting.
	_enter_idle()


# ── _PROCESS ──────────────────────────────────────────────────────────────────
# Runs every frame. Responsible for:
#   - Counting down timers in every state
#   - Triggering state transitions when timers expire
#   - Checking win condition during CHALLENGE state

func _process(delta: float) -> void:
	# Always tick the timer down, regardless of state.
	timer -= delta

	match state:

		State.IDLE:
			# Show countdown to next challenge so the player isn't staring at nothing.
			timer_label.text = "Next challenge in: %d" % ceili(timer)

			# When the idle timer expires, start a challenge round.
			if timer <= 0.0:
				_enter_challenge()

		State.CHALLENGE:
			# Show the live countdown so the player feels urgency.
			timer_label.text = "Time left: %d" % ceili(timer)

			# ── WIN CONDITION CHECK ───────────────────────────────────────────
			# Calculate the real rise from baseline.
			# real_rise is the raw °F gained since the challenge started.
			var real_rise := current_temp - baseline_temp

			# Map real_rise onto the fake display scale.
			# If real_rise = 0.0  → fake_display = 0.0  (no progress)
			# If real_rise = 0.25 → fake_display = 15.0 (halfway)
			# If real_rise = 0.5+ → fake_display = 30.0 (full bar, win!)
			# Formula: (real_rise / REAL_THRESHOLD) * FAKE_TARGET
			# This is a standard lerp/remap. The ratio into the real range
			# is applied to the fake range to get the display value.
			var fake_display : float = clamp(
				(real_rise / REAL_THRESHOLD) * FAKE_TARGET,
				0.0,
				FAKE_TARGET
			)

			# Push the visual display values every frame so the bar updates live.
			thermal_bar.value      = fake_display
			temp_display_label.text = "+%.1f°F" % fake_display

			# Has the player achieved the real threshold?
			if real_rise >= REAL_THRESHOLD:
				_enter_result(true)   # true = win
				return

			# Did time run out before they managed it?
			if timer <= 0.0:
				_enter_result(false)  # false = lose

		State.RESULT:
			# Hold the result screen for a couple of seconds, then loop back.
			if timer <= 0.0:
				_enter_idle()


# ── TELEMETRY HANDLER ─────────────────────────────────────────────────────────
# Called by the hub ~33 times per second whenever a serial packet arrives.
# We only care about the temperature argument here — everything else is ignored.
# The underscore prefix on unused parameters is a GDScript convention meaning
# "I know this exists but I'm intentionally not using it."

func _on_telemetry_updated(
		_motion: String,
		temperature: String,
		_joy_x: String,
		_joy_y: String,
		_button: String
) -> void:
	# float() converts the string "72.5" to the number 72.5.
	# If the packet is malformed and float() gets garbage, it returns 0.0 —
	# that's acceptable because the challenge check uses relative change, not absolutes.
	var parsed := float(temperature)

	# Reject physically impossible readings.
	# The DHT11 measures -4°F to 158°F. Anything outside that is sensor noise or
	# a bad packet. We don't want a corrupt reading to accidentally trigger a win.
	if parsed < -4.0 or parsed > 158.0:
		return

	current_temp = parsed

	# Mark that we have at least one valid reading.
	# _enter_challenge() checks this before allowing a round to start.
	if not has_first_reading:
		has_first_reading = true


# ── STATE TRANSITION FUNCTIONS ────────────────────────────────────────────────
# Each _enter_X function is responsible for:
#   1. Setting the state enum
#   2. Resetting the timer for that state's duration
#   3. Updating all UI elements to match the new state
# Keeping transitions in their own functions means _process stays clean.

func _enter_idle() -> void:
	state = State.IDLE
	timer = IDLE_DURATION

	# Reset the bar and display so it doesn't look like a previous result.
	thermal_bar.value       = 0.0
	temp_display_label.text = "+0.0°F"

	# Tell the player a challenge is coming. The timer_label updates live in _process.
	challenge_label.text    = "Standby... the ship systems are nominal."
	challenge_label.modulate = Color.WHITE
	timer_label.modulate     = Color.WHITE


func _enter_challenge() -> void:
	# Don't start if we haven't gotten a temperature reading yet.
	# This prevents challenges from firing before the sensor connects.
	if not has_first_reading:
		# Push the idle timer out a bit and try again.
		timer = 2.0
		return

	state = State.CHALLENGE
	timer = CHALLENGE_DURATION

	# Snapshot the current temperature as our baseline.
	# All rise calculations during this round compare against this value.
	# This is why rubbing the sensor before the challenge starts doesn't count —
	# the baseline resets fresh each round.
	baseline_temp = current_temp

	# Reset the visual bar so it starts at zero for this round.
	thermal_bar.value       = 0.0
	temp_display_label.text = "+0.0°F"

	# The flavour text the player sees. They have no idea 30°F = 0.5°F real.
	challenge_label.text = (
		"⚠ THE SPACESHIP IS TOO COLD!\n" +
		"Heat it up before you lose valuable thermal energy!\n" +
		"Raise temperature by 30°F!"
	)
	challenge_label.modulate = Color(1.0, 0.6, 0.0)  # Orange — urgent but not game over


func _enter_result(won: bool) -> void:
	state = State.RESULT
	# Show the result for 3 seconds before looping back to idle.
	timer = 3.0

	if won:
		challenge_label.text    = "✓ Thermal systems restored. Well done!"
		challenge_label.modulate = Color.GREEN
		# Lock the bar at full so it looks satisfying.
		thermal_bar.value        = FAKE_TARGET
		temp_display_label.text  = "+%.1f°F" % FAKE_TARGET
		timer_label.text         = "SUCCESS"
		timer_label.modulate     = Color.GREEN
	else:
		challenge_label.text    = "✗ Thermal energy lost. The ship is freezing."
		challenge_label.modulate = Color.RED
		# Drop the bar to zero — opportunity missed.
		thermal_bar.value        = 0.0
		temp_display_label.text  = "+0.0°F"
		timer_label.text         = "FAILED"
		timer_label.modulate     = Color.RED
