extends Control

# Signals for game loop
signal DepleteHeart
signal AllBarcodesScanned # Emits every time a set of targets is successfully completed

# Game Configuration
const TARGET_COUNT: int = 10 # How many correct scans per loop cycle
var codes_remaining: int = TARGET_COUNT

# The current single barcode number the user needs to find (from 1 to 30)
var active_target: int = 0

var barcode_buffer: String = ""

# Onready variables automatically find your nodes inside the LayoutContainer
@onready var codes_remaining_label: Label = $LayoutContainer/CodesRemainingLabel
@onready var target_barcode_label: Label = $LayoutContainer/TargetBarcodeLabel

func _ready() -> void:
	randomize() # Seeds the random number generator
	start_new_cycle()

func start_new_cycle() -> void:
	codes_remaining = TARGET_COUNT
	pick_random_target()
	update_ui()

func pick_random_target() -> void:
	# Pure randomness: picks a number between 1 and 30
	active_target = randi_range(1, 30)

func update_ui() -> void:
	# Update the left label
	codes_remaining_label.text = "SCAN " + str(codes_remaining) + " MORE BARCODES\nFOR AN EXTRA LIFE"
	
	# Update the right label
	target_barcode_label.text = "NEXT:\n" + str(active_target)

func _input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed:
		var keycode = event.keycode
		
		if keycode == KEY_ENTER:
			if barcode_buffer != "":
				on_barcode_scanned(barcode_buffer.strip_edges())
				barcode_buffer = ""
		elif keycode == KEY_BACKSPACE:
			barcode_buffer = barcode_buffer.left(barcode_buffer.length() - 1)
		else:
			if event.unicode != 0:
				barcode_buffer += char(event.unicode)

func on_barcode_scanned(barcode: String) -> void:
	print("Scanned: ", barcode)
	
	# Convert scanned string to an integer
	var scanned_num = barcode.to_int()
	
	# Strip down full barcode structure (100000001 - 100000030) to its 1-30 equivalent
	if scanned_num >= 100000001 and scanned_num <= 100000030:
		scanned_num = scanned_num - 100000000

	# Check match
	if scanned_num == active_target:
		print("Correct scan!")
		codes_remaining -= 1
		
		if codes_remaining <= 0:
			print("Cycle complete! Emitting signal and refreshing loop...")
			AllBarcodesScanned.emit()
			
			# This creates the infinite loop: resets the count back to TARGET_COUNT instantly
			start_new_cycle() 
		else:
			# Pure randomness: pick a new target immediately (even if it rolls the same number)
			pick_random_target()
			update_ui()
	else:
		print("Wrong barcode! Emitting DepleteHeart.")
		DepleteHeart.emit()
