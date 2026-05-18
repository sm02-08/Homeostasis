extends Control

@onready var time_label: Label = $TimeLabel

# Call this method from Main before making the screen visible
func set_survival_time(total_seconds: float) -> void:
	var minutes: int = int(total_seconds) / 60
	var seconds: int = int(total_seconds) % 60
	time_label.text = "YOU SURVIVED FOR\n%02d:%02d" % [minutes, seconds]

func _on_restart_button_pressed() -> void:
	# Reloads the current active main scene entirely, resetting all minigames and health
	get_tree().reload_current_scene()
