extends Control

# --- PHASE 2: Core Data Structures & State Management ---
enum GameState { IDLE, FLASHING, PLAYER_TURN }
var current_state: GameState = GameState.IDLE

var master_sequence: Array[int] = []
var player_step: int = 0

# Node References
@onready var grid_container: GridContainer = $GridContainer
@onready var flash_timer: Timer = $FlashTimer
@onready var status_label: Label = $StatusLabel

# Color Constants for Flashing Effects
const COLOR_OFF: Color = Color.DIM_GRAY
const COLOR_FLASH: Color = Color.GREEN_YELLOW
const COLOR_ERROR: Color = Color.CRIMSON

# --- PHASE 4: Serial Communication Configuration ---
var serial_file: FileAccess = null
# Windows example: "\\\\.\\COM3" (The extended path prefix handles ports above COM9 reliably)
# Mac/Linux example: "/dev/ttyACM0" or "/dev/tty.usbmodem14101"
@export var com_port_path: String = "\\\\.\\COM3"

func _ready() -> void:
	# Initialize UI button visual states and manual mouse click connections
	initialize_grid_buttons()
	
	# Initialize and open connection to the physical hardware
	setup_serial_connection()
	
	# Start the game loop automatically by generating the first step
	start_new_game()

func _process(_delta: float) -> void:
	# Continuous execution tracking to pull data off the hardware buffer
	read_serial_data()

func initialize_grid_buttons() -> void:
	for i in range(16):
		var btn: Button = get_node("GridContainer/Button_" + str(i)) as Button
		if btn:
			btn.modulate = COLOR_OFF
			
			# Connects manual mouse clicks for prototyping without hardware
			# Using a lambda function to pass the exact button index safely
			btn.pressed.connect(func(): on_square_input_received(i))

func setup_serial_connection() -> void:
	if com_port_path.is_empty():
		print("Serial port path is empty. Running in mouse-only mode.")
		return
		
	# Open the serial port as a raw read/write data stream
	serial_file = FileAccess.open(com_port_path, FileAccess.READ_WRITE)
	if serial_file:
		print("Serial communication established successfully on: ", com_port_path)
	else:
		# Accessing OS ports directly via file streams can sometimes be restricted by OS permissions
		print("Failed to open serial port: ", com_port_path, ". Ensure the port is correct and the Arduino IDE serial monitor is closed.")

func start_new_game() -> void:
	master_sequence.clear()
	status_label.text = "Press any key on the pad to start!"
	current_state = GameState.IDLE

# --- PHASE 2: The Flashing Loop (Asynchronous) ---
func advance_sequence() -> void:
	current_state = GameState.FLASHING
	status_label.text = "Watch carefully..."
	player_step = 0
	
	# Append a new randomized target index (0-15) to the sequence list
	master_sequence.append(randi() % 16)
	
	# Iterate sequentially through the array without blocking the main game thread
	for index in master_sequence:
		var target_button: Button = get_node("GridContainer/Button_" + str(index)) as Button
		
		# Switch button to its highlighted state
		target_button.modulate = COLOR_FLASH
		flash_timer.start()
		
		# Halt execution flow cleanly inside this coroutine until the timer finishes
		await flash_timer.timeout
		
		# Revert button back to default state
		target_button.modulate = COLOR_OFF
		flash_timer.start()
		
		# Brief buffer delay between sequential flashes so back-to-back duplicate keys are distinct
		await flash_timer.timeout
		
	# Hand over control permissions to the player
	current_state = GameState.PLAYER_TURN
	status_label.text = "Your turn! Replicate the pattern."

# --- PHASE 2 & 4: Input Routing & Validation Logic ---
func on_square_input_received(input_index: int) -> void:
	# Guard Clause: Instantly ignore inputs if the game is resetting or displaying a pattern
	if current_state == GameState.FLASHING:
		return
		
	# If game is sitting idle, any initial keystroke triggers the pattern sequence loop
	if current_state == GameState.IDLE:
		advance_sequence()
		return
		
	# Validate player input against the step index of the master array
	if input_index == master_sequence[player_step]:
		# Valid press logic tracking
		flash_button_briefly(input_index, COLOR_FLASH)
		player_step += 1
		
		# Check if the entire sequence length has been safely cleared
		if player_step >= master_sequence.size():
			status_label.text = "Correct! Get ready for the next level..."
			
			# Create a brief one-shot pause before expanding the difficulty sequence
			await get_tree().create_timer(1.0).timeout
			advance_sequence()
	else:
		# Invalid press logic tracking
		trigger_failure_state()

func flash_button_briefly(index: int, color: Color) -> void:
	var btn: Button = get_node("GridContainer/Button_" + str(index)) as Button
	btn.modulate = color
	await get_tree().create_timer(0.25).timeout
	btn.modulate = COLOR_OFF

func trigger_failure_state() -> void:
	current_state = GameState.FLASHING # Locks input out completely
	status_label.text = "Game Over! Resetting..."
	
	# Visual error feedback across the entire 4x4 matrix layout
	for i in range(16):
		get_node("GridContainer/Button_" + str(i)).modulate = COLOR_ERROR
		
	flash_timer.start()
	await flash_timer.timeout
	
	for i in range(16):
		get_node("GridContainer/Button_" + str(i)).modulate = COLOR_OFF
		
	# Fully scrub game data structures back to base level configuration
	start_new_game()

# --- PHASE 4: Background Serial Polling Logic ---
func read_serial_data() -> void:
	# Safety guard check to verify the port was successfully mapped as a file
	if serial_file == null:
		return
		
	# Check if there is new unread line text sitting in the OS hardware buffer stream
	if serial_file.get_length() > 0:
		var raw_data: String = serial_file.get_line().strip_edges()
		
		# Ensure the parsed line isn't empty before performing data conversion
		if not raw_data.is_empty() and raw_data.is_valid_int():
			var pressed_index: int = raw_data.to_int()
			
			# Double check values fall explicitly within bounds before routing data
			if pressed_index >= 0 and pressed_index <= 15:
				on_square_input_received(pressed_index)
