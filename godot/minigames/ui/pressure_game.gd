extends Control

# Signal following your exact main scene naming convention
signal DepleteHeart

# Game Configuration Constants
const MAX_PRESSES: int = 30
const WARNING_THRESHOLD: int = 7
const RESET_VALUE: int = 25

# Adjustable configuration via inspector
@export var depletion_rate_seconds: float = 1.0

# Game State
var current_presses: int = RESET_VALUE
var last_button_state: String = "0"  # Tracks transitions ("0" -> "1")
var depletion_timer: float = 0.0
var flash_timer: float = 0.0

# Node References
@onready var pie_progress_bar: TextureProgressBar = $PieProgressBar
@onready var status_label: Label = $StatusLabel
@onready var hub: Node = $"/root/CentralHardwareHub"

func _ready() -> void:
	# Connect to the hardware hub telemetry updates
	if hub:
		hub.TelemetryUpdated.connect(_on_hardware_telemetry_updated)
	else:
		print("PressureGame: CentralHardwareHub Autoload singleton not found!")
		
	# Setup initial state
	_update_ui()


func _process(delta: float) -> void:
	# 1. Handle Automatic Depletion Timer
	depletion_timer += delta
	if depletion_timer >= depletion_rate_seconds:
		depletion_timer = 0.0
		_modify_press_amount(-1)

	# 2. Handle Flashing Effect when danger thresholds are breached
	if current_presses <= WARNING_THRESHOLD:
		flash_timer += delta
		# Alternates visibility state every 0.25 seconds
		if flash_timer >= 0.25:
			flash_timer = 0.0
			pie_progress_bar.visible = !pie_progress_bar.visible
	else:
		# Ensure progress bar is strictly visible if above the warning limit
		pie_progress_bar.visible = true


# Intercepts hardware packet updates 
func _on_hardware_telemetry_updated(motionStatus: String, temperature: String, joyX: String, joyY: String, buttonState: String) -> void:
	# EDGE DETECTION: Only register a press if state changes from released ("0") to pressed ("1")
	if buttonState == "1" and last_button_state == "0":
		_modify_press_amount(1)
		
	# Cache state for the next frame iteration check
	last_button_state = buttonState


# Handles state changes safely inside bounds
func _modify_press_amount(amount: int) -> void:
	current_presses += amount
	
	# Clamp upper bounds to ensure it never exceeds the max limit
	if current_presses > MAX_PRESSES:
		current_presses = MAX_PRESSES
		
	# Handle Critical Loss Event
	if current_presses <= 0:
		print("PressureGame: Hits 0! Depleting heart and resetting context.")
		DepleteHeart.emit()
		current_presses = RESET_VALUE
		pie_progress_bar.visible = true # Reset flash safety visibility
		
	_update_ui()


func _update_ui() -> void:
	pie_progress_bar.value = current_presses
	if status_label:
		status_label.text = str(current_presses) + " / " + str(MAX_PRESSES)
