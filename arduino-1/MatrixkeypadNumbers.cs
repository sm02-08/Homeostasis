using Godot;
using System;
using System.IO.Ports;

public partial class MatrixkeypadNumbers : Node2D
{
	SerialPort serialPort; 
	RichTextLabel text; 

	public override void _Ready()
	{
		// this matches the RichTextLabel node in screen but if u change it (e.g. to like "text") then change the name here as well
		text = GetNode<RichTextLabel>("RichTextLabel"); 
		
		serialPort = new SerialPort(); 
		serialPort.PortName = "COM5"; // adjusted to actual device port on arduino ide
		serialPort.BaudRate = 9600; 
		serialPort.ReadTimeout = 50; 
		
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

		while (serialPort.BytesToRead > 0)
		{
			try
			{
				// read the lines from arduino (e.g. the "1 rpessed" stuff) and output it
				string rawMessage = serialPort.ReadLine().Trim();
				
				// msg splits at underscore
				string[] parts = rawMessage.Split('_');

				if (parts.Length == 2)
				{
					string keyLabel = parts[0];   // e.g., "1"
					string keyAction = parts[1];  // e.g., "PRESSED" or "RELEASED"

					// format "PRESSED" -> "pressed" or "RELEASED" -> "released"
					string formattedAction = keyAction.ToLower();

					// combine text in output
					text.Text = $"{keyLabel} {formattedAction}";
				}
			}
			catch (TimeoutException)
			{
				// wait for serial ticks
			}
			catch (Exception e)
			{
				GD.PrintErr($"Error parsing serial data: {e.Message}");
			}
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
