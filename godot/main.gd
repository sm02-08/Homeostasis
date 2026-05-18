extends Node

@onready var health_ui: Control = $HealthUI
@onready var barcode_game: Control = $BarcodeGame
@onready var debris_minigame: Control = $DebrisMotionMinigame
@onready var pressure_game: Control = $PressureGame
@onready var game_over_screen: Control = $GameOverScreen

# Time tracking state
var time_elapsed: float = 0.0
var is_game_over: bool = false

func _ready() -> void:
	# Connect your minigames to the depletion system
	barcode_game.DepleteHeart.connect(_on_deplete_heart_received)
	barcode_game.AllBarcodesScanned.connect(_on_all_barcodes_scanned)
	debris_minigame.DepleteHeart.connect(_on_deplete_heart_received)
	pressure_game.DepleteHeart.connect(_on_deplete_heart_received)


func _process(delta: float) -> void:
	# Track survival time as long as the player is still breathing
	if not is_game_over:
		time_elapsed += delta


func _on_deplete_heart_received() -> void:
	if is_game_over:
		return
		
	print("Main: Damage signal received.")
	health_ui.deplete_heart()
	
	# Check if the player is dead by reading the HealthUI's current_health variable
	if health_ui.current_heart_index >= 3:
		trigger_game_over()


func _on_all_barcodes_scanned() -> void:
	if not is_game_over:
		health_ui.replenish_heart()


func trigger_game_over() -> void:
	is_game_over = true
	print("Main: Game Over! Total time survived: ", time_elapsed)
	
	# Pass the final clock time to the game over screen
	game_over_screen.set_survival_time(time_elapsed)
	
	# Reveal the Game Over Screen canvas layer over everything else
	game_over_screen.visible = true
	
	# Optional: Freeze the minigames processing in the background so they stop running
	barcode_game.process_mode = PROCESS_MODE_DISABLED
	pressure_game.process_mode = PROCESS_MODE_DISABLED
