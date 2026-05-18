using Godot;
using System;
using System.IO.Ports;

public partial class CentralHardwareHub : Node2D
{
	// --- Signals that any scene can subscribe to ---
	[Signal] public delegate void MotionDetectedEventHandler();
	[Signal] public delegate void MotionClearedEventHandler();

	// TelemetryUpdated is the hub's only broadcast — raw data, no interpretation.
	// Minigames subscribe to this and extract whatever field they care about.
	// REMOVED: ButtonPressed and ButtonReleased — those were oxygen minigame specific.
	[Signal] public delegate void TelemetryUpdatedEventHandler(
		string motionStatus, string temperature, string joyX, string joyY, string buttonState);

	SerialPort serialPort;
	RichTextLabel telemetryList;

	private bool _lastMotionState = false;
	// REMOVED: _lastButtonState — was only needed for ButtonPressed/ButtonReleased edge detection

	public override void _Ready()
	{
		telemetryList = GetNodeOrNull<RichTextLabel>("RichTextLabel");

		serialPort = new SerialPort();
		serialPort.PortName = "COM4";
		serialPort.BaudRate = 9600;
		serialPort.ReadTimeout = 20;

		try
		{
			serialPort.Open();
			GD.Print("CentralHardwareHub: Serial port opened successfully.");
		}
		catch (Exception e)
		{
			GD.PrintErr($"Serial pipeline failed to initialize: {e.Message}");
		}
	}

	public override void _Process(double delta)
	{
		if (serialPort == null || !serialPort.IsOpen) return;

		while (serialPort.BytesToRead > 0)
		{
			try
			{
				string rawPacket = serialPort.ReadLine().Trim();
				string[] data = rawPacket.Split(',');

				if (data.Length == 5)
				{
					string motionStatus = data[0];
					string temperature  = data[1];
					string joyX        = data[2];
					string joyY        = data[3];
					string buttonState = data[4];

					// Broadcast all raw values — any scene can subscribe and pick what it needs
					EmitSignal(SignalName.TelemetryUpdated, motionStatus, temperature, joyX, joyY, buttonState);

					bool motionNow = (motionStatus == "MOTION_DETECTED");
					if (motionNow && !_lastMotionState)
						EmitSignal(SignalName.MotionDetected);
					else if (!motionNow && _lastMotionState)
						EmitSignal(SignalName.MotionCleared);
					_lastMotionState = motionNow;

					// REMOVED: All button edge detection (buttonNow, _lastButtonState comparison,
					// ButtonPressed/ButtonReleased emissions) — belonged to oxygen minigame only

					if (telemetryList != null)
					{
						string listOutput = "-- LIVE HARDWARE HUB --\n\n";
						listOutput += motionNow
							? "* [color=red]Motion Status: MOTION DETECTED![/color]\n"
							: "* Motion Status: Stable (No Motion)\n";
						listOutput += $"* Temperature: {temperature}°F\n";
						listOutput += $"* Joystick X-Axis: {joyX} / 1023\n";
						listOutput += $"* Joystick Y-Axis: {joyY} / 1023\n";
						listOutput += (buttonState == "1")
							? "* [color=green]Arcade Button: PRESSED![/color]\n"
							: "* Arcade Button: Released\n";
						telemetryList.Text = listOutput;
					}
				}
			}
			catch (TimeoutException) { }
			catch (Exception e)
			{
				GD.PrintErr($"Packet parsing drop: {e.Message}");
			}
		}
	}

	public override void _ExitTree()
	{
		if (serialPort != null && serialPort.IsOpen)
			serialPort.Close();
	}
}
