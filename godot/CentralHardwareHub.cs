using Godot;
using System;
using System.IO.Ports;

public partial class CentralHardwareHub : Node2D
{
	SerialPort serialPort;
	RichTextLabel telemetryList;

	public override void _Ready()
	{
		// Make sure this path matches your exact RichTextLabel node name in your scene tree!
		telemetryList = GetNode<RichTextLabel>("RichTextLabel");

		serialPort = new SerialPort();
		serialPort.PortName = "COM5"; // Check your actual port inside Arduino IDE (e.g., COM3, COM4)!
		serialPort.BaudRate = 9600;
		serialPort.ReadTimeout = 20; // Short timeout keeps the game loop running fast

		try 
		{
			serialPort.Open();
		}
		catch (Exception e) 
		{
			GD.PrintErr($"Serial pipeline failed to initialize: {e.Message}");
		}
	}

	public override void _Process(double delta)
	{
		if (serialPort == null || !serialPort.IsOpen) return;

		// Process all incoming telemetry streams
		while (serialPort.BytesToRead > 0)
		{
			try
			{
				// Read a complete line up to the newline character and strip spaces
				string rawPacket = serialPort.ReadLine().Trim();
				string[] data = rawPacket.Split(',');

				// Ensure all 4 expected data streams arrived intact
				if (data.Length == 4)
				{
					string motionStatus = data[0];
					string temperature = data[1];
					string joyX = data[2];
					string joyY = data[3];

					// Format the incoming data into a clean list layout
					string listOutput = "-- LIVE HARDWARE HUB --\n\n";
					
					// Format Motion Status with colors
					if (motionStatus == "MOTION_DETECTED")
					{
						listOutput += "* [color=red]Motion Status: MOTION DETECTED!\n";
					}
					else
					{
						listOutput += "* Motion Status: Stable (No Motion)\n";
					}

					listOutput += $"* Temperature: {temperature}°F\n";
					listOutput += $"* Joystick X-Axis: {joyX} / 1023\n";
					listOutput += $"* Joystick Y-Axis: {joyY} / 1023\n";

					// Push the entire list update to the screen at once
					telemetryList.Text = listOutput;
				}
			}
			catch (TimeoutException) 
			{
				// Normal behavior when catching trailing serial clock cycles
			}
			catch (Exception e) 
			{
				GD.PrintErr($"Packet parsing drop: {e.Message}");
			}
		}
	}

	// Always release the COM port when exiting the game so it doesn't lock up
	public override void _ExitTree()
	{
		if (serialPort != null && serialPort.IsOpen)
		{
			serialPort.Close();
		}
	}
}
