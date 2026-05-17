using Godot;
using System;
using System.IO.Ports;

public partial class UltrasonicsensorHcsro4 : Node2D
{
	SerialPort serialPort; 
	RichTextLabel text; 

	public override void _Ready()
	{
		text = GetNode<RichTextLabel>("RichTextLabel"); 
		
		serialPort = new SerialPort(); 
		serialPort.PortName = "COM5"; // this needs to be changed to be adjusted to the actual port each time in arduino ide
		serialPort.BaudRate = 9600; 
		serialPort.ReadTimeout = 50; // if data is incomplete the game doesnt just freeze completely
		
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
				// strip spaces and 
				string rawMessage = serialPort.ReadLine().Trim();
				
				// split msg at comma: index 0 is status, index 1 is distance
				string[] dataTokens = rawMessage.Split(',');

				if (dataTokens.Length == 2)
				{
					string pathStatus = dataTokens[0];
					string distanceValue = dataTokens[1];

					// format and output text directly as a RichTextLabel
					if (pathStatus == "CLEAR")
					{
						text.Text = $"Path is clear. Nearest object is {distanceValue} cm away.";
					}
					else if (pathStatus == "BLOCKED")
					{
						text.Text = $"Path is NOT clear! Nearest object is {distanceValue} cm away.";
					}
				}
			}
			catch (TimeoutException)
			{
				// if ReadLine reaches the limit before finishing run this
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
