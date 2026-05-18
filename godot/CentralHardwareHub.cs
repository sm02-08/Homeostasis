using Godot;
using System;
using System.IO.Ports;

public partial class CentralHardwareHub : Node2D
{
	// --- Signals that any scene can subscribe to ---
	[Signal] public delegate void MotionDetectedEventHandler();
	[Signal] public delegate void MotionClearedEventHandler();
	[Signal] public delegate void TelemetryUpdatedEventHandler(
		string motionStatus, string temperature, string joyX, string joyY);

	SerialPort serialPort;
	RichTextLabel telemetryList;

	// Track last motion state so we only fire signals on CHANGES, not every frame
	private bool _lastMotionState = false;

	public override void _Ready()
	{
		// This label is optional now — you can keep it for debugging or remove it
		// If you keep the label in the scene, this works. If not, guard it:
		telemetryList = GetNodeOrNull<RichTextLabel>("RichTextLabel");

		serialPort = new SerialPort();
		serialPort.PortName = "COM5"; // ← Update to your port
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

				if (data.Length == 4)
				{
					string motionStatus = data[0];
					string temperature  = data[1];
					string joyX        = data[2];
					string joyY        = data[3];

					// --- Emit broad telemetry signal every update ---
					EmitSignal(SignalName.TelemetryUpdated, motionStatus, temperature, joyX, joyY);

					// --- Only emit motion signals when state CHANGES ---
					bool motionNow = (motionStatus == "MOTION_DETECTED");

					if (motionNow && !_lastMotionState)
					{
						EmitSignal(SignalName.MotionDetected);
					}
					else if (!motionNow && _lastMotionState)
					{
						EmitSignal(SignalName.MotionCleared);
					}

					_lastMotionState = motionNow;

					// --- Update debug label if it exists ---
					if (telemetryList != null)
					{
						string listOutput = "-- LIVE HARDWARE HUB --\n\n";
						listOutput += motionNow
							? "* [color=red]Motion Status: MOTION DETECTED![/color]\n"
							: "* Motion Status: Stable (No Motion)\n";
						listOutput += $"* Temperature: {temperature}°F\n";
						listOutput += $"* Joystick X-Axis: {joyX} / 1023\n";
						listOutput += $"* Joystick Y-Axis: {joyY} / 1023\n";
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
