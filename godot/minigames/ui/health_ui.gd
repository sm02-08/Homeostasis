extends Control

# Store the hearts in an array in the order you want them removed
@onready var hearts: Array = [
	$Heart1,
	$Heart2,
	$Heart3
]

var current_heart_index: int = 0

func deplete_heart() -> void:
	# Safety check: if we are already out of hearts, do nothing
	if current_heart_index >= hearts.size():
		print("HealthUI: No hearts left to deplete!")
		return
		
	# Grab the correct heart and hide it
	var heart_to_remove = hearts[current_heart_index]
	heart_to_remove.visible = false
	
	print("HealthUI: Removed ", heart_to_remove.name)
	
	# Move to the next heart for the next call
	current_heart_index += 1

# Inside HealthUI.gd

func replenish_heart() -> void:
	# Safety check: If we are already at max health (3 hearts), do nothing
	if current_heart_index <= 0:
		print("HealthUI: Already at maximum health!")
		return
		
	# Move the index back by one to find the heart that was most recently lost
	current_heart_index -= 1
	
	# Grab that heart and make it visible again
	var heart_to_restore = hearts[current_heart_index]
	heart_to_restore.visible = true
	
	print("HealthUI: Restored ", heart_to_restore.name)
