using Godot;
using System;
using System.IO.Ports;

public partial class CentralHardwareHub : Node2D
{
	// --- Signals that any scene can subscribe to ---
	[Signal] public delegate void MotionDetectedEventHandler();
	[Signal] public delegate void MotionClearedEventHandler();
	
	[Signal] public delegate void TelemetryUpdatedEventHandler(
		string motionStatus, string temperature, string joyX, string joyY, string buttonState);

	SerialPort serialPort;
	RichTextLabel telemetryList;

	private bool _lastMotionState = false;
	// REMOVED: _lastButtonState — was only needed for ButtonPressed/ButtonReleased edge detection

	public override void _Ready()
	{
		telemetryList = GetNodeOrNull<RichTextLabel>("RichTextLabel");
		GD.Print("CentralHardwareHub: Initializing...");

		serialPort = new SerialPort();
		serialPort.PortName = "COM4";
		serialPort.BaudRate = 9600;
		serialPort.ReadTimeout = 20;

		try
		{
			serialPort.Open();
			GD.Print("CentralHardwareHub: SUCCESS! Serial port 'COM4' opened successfully.");
		}
		catch (Exception e)
		{
			GD.PrintErr($"CentralHardwareHub: CRITICAL FAILURE! Serial pipeline failed to initialize: {e.Message}");
		}
	}

	public override void _Process(double delta)
	{
		if (serialPort == null)
		{
			// Rare edge case check
			GD.PrintErr("CentralHardwareHub _Process: serialPort object is null!");
			return;
		}

		if (!serialPort.IsOpen) 
		{
			// Uncomment this line if you suspect the port is closing mid-execution:
			// GD.PrintErr("CentralHardwareHub _Process: Serial port is closed.");
			return;
		}

		// --- GATE 1: Check if bytes are physically landing in the buffer ---
		int bytesAvailable = serialPort.BytesToRead;
		if (bytesAvailable > 0)
		{
			GD.Print($"CentralHardwareHub: Buffer active! Found {bytesAvailable} bytes to read.");
		}

		while (serialPort.BytesToRead > 0)
		{
			try
			{
				GD.Print("CentralHardwareHub: Attempting to ReadLine()...");
				string rawPacket = serialPort.ReadLine();
				
				// --- GATE 2: Print exactly what raw text arrived before cleaning it ---
				GD.Print($"CentralHardwareHub: [RAW PACKET RECEIVED] -> '{rawPacket}' (Length: {rawPacket.Length})");

				rawPacket = rawPacket.Trim();
				string[] data = rawPacket.Split(',');

				// --- GATE 3: Debug the structural parsing logic ---
				GD.Print($"CentralHardwareHub: Split packet into {data.Length} elements.");
				for (int i = 0; i < data.Length; i++)
				{
					GD.Print($"  -> Element [{i}]: '{data[i]}'");
				}

				if (data.Length == 5)
				{
					string motionStatus = data[0];
					string temperature  = data[1];
					string joyX         = data[2];
					string joyY         = data[3];
					string buttonState  = data[4]; 

					GD.Print($"CentralHardwareHub: Packet structure valid! Parsing: Motion={motionStatus}, Temp={temperature}, X={joyX}, Y={joyY}, Btn={buttonState}");

					EmitSignal(SignalName.TelemetryUpdated, motionStatus, temperature, joyX, joyY, buttonState);

					bool motionNow = (motionStatus == "MOTION_DETECTED");
					if (motionNow && !_lastMotionState)
					{
						GD.Print("CentralHardwareHub: Signal Fired -> MotionDetected");
						EmitSignal(SignalName.MotionDetected);
					else if (!motionNow && _lastMotionState)
					{
						GD.Print("CentralHardwareHub: Signal Fired -> MotionCleared");
						EmitSignal(SignalName.MotionCleared);
					_lastMotionState = motionNow;

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
				else
				{
					GD.PrintErr($"CentralHardwareHub: BAD LAYOUT! Expected 5 fields but got {data.Length}. Verify your Arduino Serial.println format.");
				}
			}
			catch (TimeoutException) 
			{
				// ReadTimeout is 20ms, so timeouts are normal if data isn't perfectly continuous.
				// Uncomment below only if you suspect total communication dropouts:
				// GD.Print("CentralHardwareHub: ReadLine() timed out.");
			}
			catch (Exception e)
			{
				GD.PrintErr($"CentralHardwareHub: Error processing stream loop: {e.Message}");
			}
		}
	}

	public override void _ExitTree()
	{
		GD.Print("CentralHardwareHub: Cleaning up and shutting down serial port connections.");
		if (serialPort != null && serialPort.IsOpen)
		{
			serialPort.Close();
			GD.Print("CentralHardwareHub: Serial port closed gracefully.");
		}
	}
}
