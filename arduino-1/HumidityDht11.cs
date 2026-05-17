using Godot;
using System;
using System.IO.Ports;

public partial class HumidityDht11 : Node2D
{
	SerialPort serialPort; 
	RichTextLabel text; 

	public override void _Ready()
	{
		text = GetNode<RichTextLabel>("RichTextLabel"); 
		
		serialPort = new SerialPort(); 
		serialPort.PortName = "COM4"; // make sure this matches the port in arduino ide
		serialPort.BaudRate = 9600; 
		
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

		if (serialPort.BytesToRead > 0)
		{
			string serialMessage = serialPort.ReadExisting(); 

			if (serialMessage.Contains("ALERT_ABOVE_80"))
			{
				text.Text = "Temperature above 80F has been hit.";
			}
			else if (serialMessage.Contains("TEMP_NORMAL"))
			{
				text.Text = "Temperature is stable.";
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
