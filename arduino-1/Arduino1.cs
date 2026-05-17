using Godot;
using System;
using System.IO.Ports;

public partial class Arduino1 : Node2D
{
	SerialPort serialPort; 
	RichTextLabel text; 

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		// execution 
		text = GetNode<RichTextLabel>("RichTextLabel");
		serialPort = new SerialPort(); 
		serialPort.PortName = "COM5"; 
		serialPort.BaudRate = 9600; 
		serialPort.Open(); // opening access to all com3 in he process 
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (serialPort.BytesToRead > 0)
		{
			string serialMessage = serialPort.ReadExisting(); 

			if (serialMessage.Contains("HelloGodot"))
			{
				text.Text = "Hello Arduino, I hear you :)";
			}
		}
	}
}
