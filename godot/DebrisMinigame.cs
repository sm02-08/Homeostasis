using Godot;
using System;

public partial class DebrisMinigame : Control
{
	// --- Node references (assigned in _Ready via GetNode) ---
	private Label       _statusLabel;
	private Label       _countdownLabel;
	private ProgressBar _progressBar;
	private Timer       _idleTimer;       // 15-second safe window between attacks
	private Timer       _countdownTimer;  // The danger window when debris hits

	// --- Tuning constants ---
	private const float IdleSeconds      = 10f;  // Time between debris events
	private const float CountdownSeconds = 15f;   // Time to clear debris before death
	private const float ProgressPerMotion = 34f; // How much each motion pulse fills the bar (0–100)

	// --- State ---
	private enum GameState { Idle, UnderAttack, Dead }
	private GameState _state = GameState.Idle;
	private float     _timeRemaining;

	// --- Hub reference ---
	private CentralHardwareHub _hub;

	public override void _Ready()
	{
		// Grab all child nodes by name (must match your scene tree exactly)
		_statusLabel    = GetNode<Label>("StatusLabel");
		_countdownLabel = GetNode<Label>("CountdownLabel");
		_progressBar    = GetNode<ProgressBar>("ProgressBar");
		_idleTimer      = GetNode<Timer>("IdleTimer");
		_countdownTimer = GetNode<Timer>("CountdownTimer");

		// Get the Autoload hub singleton
		_hub = GetNode<CentralHardwareHub>("/root/CentralHardwareHub");

		// Subscribe to motion signal from the hub
		_hub.MotionDetected += OnMotionDetected;

		// Connect timer timeouts
		_idleTimer.Timeout      += OnIdleTimerTimeout;
		_countdownTimer.Timeout += OnCountdownTimerExpired;

		// Configure timers (one-shot so they don't loop automatically)
		_idleTimer.WaitTime   = IdleSeconds;
		_idleTimer.OneShot    = true;
		_countdownTimer.WaitTime = CountdownSeconds;
		_countdownTimer.OneShot  = true;

		// Set up progress bar range
		_progressBar.MinValue = 0;
		_progressBar.MaxValue = 100;
		_progressBar.Value    = 0;

		// Start in idle state
		EnterIdle();
	}

	// Called every frame — used to update the countdown display
	public override void _Process(double delta)
	{
		if (_state == GameState.UnderAttack)
		{
			// Compute remaining time from the timer node directly (always accurate)
			float remaining = (float)_countdownTimer.TimeLeft;
			_countdownLabel.Text = $"TIME LEFT: {remaining:F1}s";
		}
	}

	// -------------------------------------------------------
	// STATE MACHINE
	// -------------------------------------------------------

	private void EnterIdle()
	{
		_state = GameState.Idle;

		_statusLabel.Text    = "All clear. Enjoy the peace...";
		_countdownLabel.Text = "";
		_progressBar.Visible = false;
		_progressBar.Value   = 0;

		_idleTimer.Start();
	}

	private void EnterUnderAttack()
	{
		_state = GameState.UnderAttack;

		_statusLabel.Text    = "⚠ DEBRIS IMPACT! WAVE YOUR HAND! ⚠";
		_progressBar.Visible = true;
		_progressBar.Value   = 0;

		_countdownTimer.Start();
		GD.Print("DebrisMinigame: Debris event started.");
	}

	private void EnterDead()
	{
		_state = GameState.Dead;

		_statusLabel.Text    = "YOU DIED.";
		_countdownLabel.Text = "";
		_progressBar.Visible = false;

		// Disconnect motion listener so nothing happens after death
		_hub.MotionDetected -= OnMotionDetected;

		GD.Print("DebrisMinigame: Player died.");
		// You can emit a signal here, change scene, show a restart button, etc.
	}

	private void EnterWon()
	{
		_statusLabel.Text    = "✓ DEBRIS CLEARED! Well done!";
		_countdownLabel.Text = "";
		_progressBar.Visible = false;

		GD.Print("DebrisMinigame: Debris cleared! Restarting idle phase.");

		// Stop the countdown — player beat it
		_countdownTimer.Stop();

		// Restart the idle phase
		EnterIdle();
	}

	// -------------------------------------------------------
	// EVENT HANDLERS
	// -------------------------------------------------------

	// Fires when the 15-second idle window ends → debris hits
	private void OnIdleTimerTimeout()
	{
		if (_state == GameState.Idle)
			EnterUnderAttack();
	}

	// Fires when the player waves their hand (motion detected)
	private void OnMotionDetected()
	{
		if (_state != GameState.UnderAttack) return;

		// Fill the progress bar by one pulse
		_progressBar.Value += ProgressPerMotion;
		GD.Print($"DebrisMinigame: Motion pulse! Progress = {_progressBar.Value}");

		// Check if bar is full
		if (_progressBar.Value >= _progressBar.MaxValue)
			EnterWon();
	}

	// Fires when the countdown timer runs out
	private void OnCountdownTimerExpired()
	{
		if (_state == GameState.UnderAttack)
			EnterDead();
	}

	// Always unsubscribe when this node leaves the tree
	public override void _ExitTree()
	{
		if (_hub != null)
			_hub.MotionDetected -= OnMotionDetected;
	}
}
