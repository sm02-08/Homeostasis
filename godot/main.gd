extends Node

# Grab references to the Health UI and both minigame nodes
@onready var health_ui: Control = $HealthUI
@onready var barcode_game: Control = $BarcodeGame
@onready var debris_minigame: Control = $DebrisMotionMinigame
@onready var pressure_game: Control = $PressureGame

func _ready() -> void:
	# 1. Listen to the GDScript Barcode game signals
	# GDScript uses lowercase snake_case for native signal connections
	barcode_game.DepleteHeart.connect(_on_deplete_heart_received)
	barcode_game.AllBarcodesScanned.connect(_on_all_barcodes_scanned)
	
	# 2. Listen to the C# Debris minigame signal
	# C# signals maintain their exact spelling from the C# code
	debris_minigame.DepleteHeart.connect(_on_deplete_heart_received)
	pressure_game.DepleteHeart.connect(_on_deplete_heart_received)

# --- SIGNAL CALLBACKS ---

# Both minigames route their damage signals right into this single function!
func _on_deplete_heart_received() -> void:
	print("Main received damage signal! Routing to HealthUI...")
	health_ui.deplete_heart()


func _on_all_barcodes_scanned() -> void:
	print("Main: Player successfully completed a barcode scan streak!")
	health_ui.replenish_heart()
