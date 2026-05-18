using Godot;
using System;

public partial class DebrisMinigame : Control
{
	[Signal]
	public delegate void DepleteHeartEventHandler();
	// --- Node references (assigned in _Ready via GetNode) ---
	private Label       _statusLabel;
	private Label       _countdownLabel;
	private ProgressBar _progressBar;
	private Timer       _idleTimer;       // 15-second safe window between attacks
	private Timer       _countdownTimer;  // The danger window when debris hits
	private TextureRect       _textureRect;  // The danger window when debris hits

	// --- Tuning constants ---
	private const float IdleSeconds      = 20f;  // Time between debris events
	private const float CountdownSeconds = 15f;   // Time to clear debris before death
	private const float ProgressPerMotion = 34f; // How much each motion pulse fills the bar (0–100)

	// --- State ---
	private enum GameState { Idle, UnderAttack, Dead }
	private GameState _state = GameState.Idle;
	private float     _timeRemaining;
	private Random    _random = new Random();

	// --- Hub reference ---
	private CentralHardwareHub _hub;

	public override void _Ready()
	{
		// Grab all child nodes by name (must match your scene tree exactly)
		_textureRect    = GetNode<TextureRect>("TextureRect");
		_statusLabel    = _textureRect.GetNode<Label>("StatusLabel");
		//_countdownLabel = GetNode<Label>("CountdownLabel");
		//_progressBar    = GetNode<ProgressBar>("ProgressBar");
		_idleTimer      = _textureRect.GetNode<Timer>("IdleTimer");
		_countdownTimer = _textureRect.GetNode<Timer>("CountdownTimer");
		
		_countdownLabel = _textureRect.GetNode<Label>("CountdownLabel");
		_progressBar    = _textureRect.GetNode<ProgressBar>("ProgressBar");

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
			_countdownLabel.Text = $"{remaining:F1} seconds remaining";
		}
	}

	// -------------------------------------------------------
	// STATE MACHINE
	// -------------------------------------------------------

	private void EnterIdle()
	{
		_state = GameState.Idle;

		//_statusLabel.Text    = "All clear. Enjoy the peace...";
		//_countdownLabel.Text = "";
		//_progressBar.Visible = false;
		//_progressBar.Value   = 0;

		// Reset visual configurations back to default visibility
		_textureRect.Modulate = Colors.White;
		_textureRect.Visible = false; 
		_progressBar.Value = 0;
		
		_idleTimer.Start();
	}

	private void EnterUnderAttack()
	{
		_state = GameState.UnderAttack;

		// Pick a random location between your bounding coordinates
		float randomX = (float)_random.NextDouble() * (1325f - 30f) + 30f;
		float randomY = (float)_random.NextDouble() * (750f - 50f) + 50f;
		_textureRect.GlobalPosition = new Vector2(randomX, randomY);

		_textureRect.Visible = true;  
		_progressBar.Value = 0;
		
		_countdownTimer.Start();
		GD.Print($"DebrisMinigame: Debris event started at location: {_textureRect.GlobalPosition}");
	}

	private void EnterDead()
	{
		_state = GameState.Dead;
		_countdownLabel.Text = "";

		GD.Print("DebrisMinigame: Player took damage. Resetting window.");
		EmitSignal(SignalName.DepleteHeart);
		
		// Instantly route back to idle so a new debris event can trigger down the line
		EnterIdle();
	}

	private void EnterWon()
	{
		// 1. Prevent the countdown timer from expiring while animating
		_countdownTimer.Stop();
		_countdownLabel.Text = "";

		// 2. Clear out any ongoing bar animations so they don't break the next round
		_progressBar.Value = _progressBar.MaxValue; 

		GD.Print("DebrisMinigame: Debris cleared! Animating win...");

		// 3. Create the parallel win animation using the TextureRect
		Tween winTween = CreateTween().SetParallel(true);
		
		// Fly up (Assuming you saved private Vector2 _originalPosition in _Ready)
		winTween.TweenProperty(_textureRect, "global_position:y", _textureRect.GlobalPosition.Y - 150f, 0.6f)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
				
		// Fade out
		winTween.TweenProperty(_textureRect, "modulate:a", 0f, 0.6f);

		// 4. CRITICAL: Only reset to Idle AFTER the animations finish moving/fading!
		winTween.Chain().TweenCallback(Callable.From(EnterIdle));
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
		double targetValue = Mathf.Min(_progressBar.Value + ProgressPerMotion, _progressBar.MaxValue);

		// Tween the progress bar's width change smoothly over 0.15 seconds
		Tween progressTween = CreateTween();
		progressTween.TweenProperty(_progressBar, "value", targetValue, 0.15f)
					 .SetTrans(Tween.TransitionType.Quad)
					 .SetEase(Tween.EaseType.Out);

		// Once the bar reaches maximum, proceed to win layout state
		if (targetValue >= _progressBar.MaxValue)
		{
			progressTween.Chain().TweenCallback(Callable.From(EnterWon));
		}
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
