using Godot;
using System;
using System.IO.Ports;

public partial class Joystick : Node2D
{
	SerialPort serialPort;
	
	[Export]
	public float Speed { get; set; } = 300.0f; // Movement speed in pixels per second

	// The joystick's physical center sits roughly around 512
	private const int CenterValue = 512;
	// Deadzone prevents the sprite from drifting due to physical sensor imperfections
	private const int Deadzone = 50; 

	public override void _Ready()
	{
		serialPort = new SerialPort();
		serialPort.PortName = "COM4"; // Update this to match your actual Arduino IDE port!
		serialPort.BaudRate = 9600;
		serialPort.ReadTimeout = 20;

		try
		{
			serialPort.Open();
		}
		catch (Exception e)
		{
			GD.PrintErr($"Could not open serial port: {e.Message}");
		}
	}

	public override void _Process(double delta)
	{
		if (serialPort == null || !serialPort.IsOpen) return;

		Vector2 direction = Vector2.Zero;

		// Read the latest lines from the Arduino buffer
		while (serialPort.BytesToRead > 0)
		{
			try
			{
				string rawMessage = serialPort.ReadLine().Trim();
				string[] tokens = rawMessage.Split(',');

				if (tokens.Length == 2)
				{
					// Convert the text tokens into integers
					int rawX = int.Parse(tokens[0]);
					int rawY = int.Parse(tokens[1]);

					// Calculate movement relative to the resting center point
					int inputX = rawX - CenterValue;
					int inputY = rawY - CenterValue;

					// Apply deadzone checking on X axis
					if (Math.Abs(inputX) > Deadzone)
					{
						// Invert inputX if the sprite moves opposite your physical hand layout
						direction.X = inputX; 
					}

					// Apply deadzone checking on Y axis
					if (Math.Abs(inputY) > Deadzone)
					{
						// Invert inputY if the sprite moves upside down relative to the stick
						direction.Y = inputY; 
					}
				}
			}
			catch (TimeoutException)
			{
				// Handled cleanly when catching trailing serial clock cycles
			}
			catch (Exception e)
			{
				GD.PrintErr($"Data parsing error: {e.Message}");
			}
		}

		// If the joystick moved beyond the deadzone, translate the Sprite
		if (direction != Vector2.Zero)
		{
			// Normalize ensures diagonal movement isn't faster than orthagonal movement
			direction = direction.Normalized();
			Position += direction * Speed * (float)delta;
		}
	}

	public override void _ExitTree()
	{
		if (serialPort != null && serialPort.IsOpen)
		{
			serialPort.Close();
		}
	}
}
