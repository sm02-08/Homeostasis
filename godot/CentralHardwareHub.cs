using Godot;
using System;
using System.IO.Ports;

public partial class CentralHardwareHub : Node2D
{
	// --- Signals that any scene can subscribe to ---
	[Signal] public delegate void MotionDetectedEventHandler();
	[Signal] public delegate void MotionClearedEventHandler();
	
	// MODIFIED: Added a fifth parameter 'string buttonState' to the broad telemetry signal 
	// so other game scripts can know if the arcade button is pressed.
	[Signal] public delegate void TelemetryUpdatedEventHandler(
		string motionStatus, string temperature, string joyX, string joyY, string buttonState);

	SerialPort serialPort;
	RichTextLabel telemetryList;

	// Track last motion state so we only fire signals on CHANGES, not every frame
	private bool _lastMotionState = false;

	public override void _Ready()
	{
		telemetryList = GetNodeOrNull<RichTextLabel>("RichTextLabel");

		serialPort = new SerialPort();
		serialPort.PortName = "COM4"; // ← Update to your port
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

				// MODIFIED: Changed strict data layout check from 4 to 5 elements.
				// If we don't change this, Godot will reject the new 5-value packet incoming from Arduino.
				if (data.Length == 5)
				{
					string motionStatus = data[0];
					string temperature  = data[1];
					string joyX        = data[2];
					string joyY        = data[3];
					
					// NEW: Extracted the 5th element (index 4) from our split string CSV array.
					string buttonState = data[4]; 

					// MODIFIED: Updated signal emission to include our newly parsed button state string.
					EmitSignal(SignalName.TelemetryUpdated, motionStatus, temperature, joyX, joyY, buttonState);

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
						
						// NEW: Appended BBCode string rendering to visually track button presses inside your UI list.
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
