using UnityEngine;
using System;
using System.Collections;

public class Player : MonoBehaviour {

	public GameObject moveControllerPrefab;

	public int team;
	public bool inGame;
	public UniMoveController controller;
	public GameObject controllerObj;
	private Color defaultColor;
	private bool hasBall;
	private DateTime hasBallStart;
	private bool hasCycle;
	private bool readyToCatch;
	private bool startedPass;
	private bool startedFumble;
	private Vector3 lastAccel = Vector3.zero;
	private IEnumerator setLEDRoutine;
	private IEnumerator setRumbleRoutine;

	private Vector3 registeredOrientation;
	private DateTime registeredOrientationStart;

	public event Action<Player> FumbleEvent;
	public event Action<Player> PassEvent;
	public event Action<Player> CatchEvent;

	public double TimeInOrientation {
		get { return DateTime.Now.Subtract(registeredOrientationStart).TotalMilliseconds; }
	}

	void Awake() {
	}

	void Start() {
	}

	public void Init(UniMoveController c, GameObject co) {
		controller = c;
		controllerObj = co;
	}

	// Update is called once per frame
	void Update () {
		if ( controllerObj != null )
			controllerObj.transform.localRotation = controller.Orientation;

		if ( !inGame ) 
			return;

		// CATCHING
		// you need to press the trigger WHILE you have the cycle
		if ( hasCycle ) {
			if ( controller.Trigger == 0 ) {
				readyToCatch = true;
			}
			if ( readyToCatch && controller.Trigger > 0.5 ) {
				CatchBall();
			}
		}

		// PASSING
		// you let go of the trigger, and then you have an amount of time to position yourself, and the pass fires
		if ( hasBall && !startedPass && controller.Trigger == 0 ) {
			Debug.Log ("passed");
			Invoke("PassBall", Game.Instance.passLength);
			CancelInvoke("FumbleBall");
			startedPass = true;
		}

		// FUMBLING
		// a jump in acceleration, or movement away from starting orientation
		float accelTolerance = Game.Instance.accelTolerance;
		float gyroTolerance = Game.Instance.gyroTolerance;
		if ( hasBall && !startedPass && !startedFumble && DateTime.Now.Subtract(hasBallStart).TotalSeconds > Game.Instance.catchMargin ) { // allow a split second after receiving before fumbling is possible
//			if ( Mathf.Abs(controller.Acceleration.x-lastAccel.x)>accelTolerance || Mathf.Abs(controller.Acceleration.y-lastAccel.y)>accelTolerance || Mathf.Abs(controller.Acceleration.z-lastAccel.z)>accelTolerance ) {

			Vector3 orientationDiff = normalizedOrientationDifference(normalizedOrientation(controller.Orientation.eulerAngles), registeredOrientation);
//			Debug.Log (orientationDiff);
//			Debug.Log (orientationDiff.magnitude);

			bool fumbled = false;

			if ( (controller.Acceleration-lastAccel).magnitude > accelTolerance ) {
				if ( !Game.Instance.suppressFumble )
					fumbled = true;
				Debug.Log("Fumbled from acceleration");
			}
			else if ( orientationDiff.magnitude > gyroTolerance ) {
				if ( !Game.Instance.suppressFumble )
					fumbled = true;
				Debug.Log("Fumbled from orientation");
			}

			if ( fumbled ) {
				Invoke("FumbleBall", Game.Instance.passMargin); // we delay the fumble, because if a pass begins in this time we'll call it a pass instead
				startedFumble = true;
			}

		}

		// while we don't have the ball, register orientation changes for the sake of receiving passes
		if ( !hasBall ) {
			Vector3 diff = normalizedOrientationDifference(normalizedOrientation(controller.Orientation.eulerAngles), normalizedOrientation(registeredOrientation));
			Vector2 diff2 = new Vector2(diff.x, diff.y);
			if ( diff2.magnitude > Game.Instance.orientationSamenessThreshold ) {
				registeredOrientation = normalizedOrientation(controller.Orientation.eulerAngles);
				registeredOrientationStart = DateTime.Now;
			}
		}

		lastAccel = controller.Acceleration;


		
		// DEBUG
		
		//		if ( hasBall ) {
		//			Debug.Log(controller.Orientation.eulerAngles);
		//			Debug.Log((controller.Orientation.eulerAngles - startOrientation));
		//			Debug.Log((controller.Orientation.eulerAngles - startOrientation).magnitude);
		//		}
		//		if ( Game.Instance.HoldingPlayer != null && Game.Instance.HoldingPlayer != this ) {
		//			Debug.Log (Game.Instance.HoldingPlayer.GetOrientationDifference(this));
		//		}
	}

	public void SetTeam(int theTeam) {
		team = theTeam;
		UpdateTeamColor();
	}

	public void UpdateTeamColor() {
		defaultColor = team == 1 ? Game.Instance.TeamColor1 : Game.Instance.TeamColor2;
		SetLED(defaultColor);
	}

	public void ResetLEDAndRumble() {
		SetLED(defaultColor);
		SetRumble(0);
	}

	public void SetEnding(bool won) {
		if ( won ) {
			Flash(Game.Instance.BallColor, defaultColor);
		} else {
			SetLED(Color.black);
		}
	}

	// STATE

	public void CycleBallOn() {
		SetLED(Game.Instance.CycleColor);
		SetRumble(0.3f);
		SetRumble(0, 0.3f);
		hasCycle = true;
		readyToCatch = false;
	}

	public void CycleBallOff() {
		SetLED(defaultColor);
		hasCycle = false;
	}

	public void GainBall() {
//		SetLED(Game.Instance.BallColor);
		Flash(Game.Instance.BallColor, defaultColor);
		hasBall = true;
		hasBallStart = DateTime.Now;
		hasCycle = false;
		startedPass = false;
		startedFumble = false;
		registeredOrientation = normalizedOrientation(controller.Orientation.eulerAngles);
	}

	public void LoseBall() {
		SetLED(Color.black);
		SetLED(defaultColor, 1);
		hasBall = false;
	}

	public void Score(int points) {
//		SetLED(Game.Instance.ScoreColor);
//		SetLED(Game.Instance.BallColor, 0.2f);
//		SetRumble(0.5f);
//		SetRumble(0, 0.2f);
	}

	// INTERNAL EVENTS

	public void CatchBall() {
		GainBall();
		SetRumble(0.5f);
		SetRumble(0, 0.3f);
		CatchEvent(this);
	}

	public void PassBall() {
		LoseBall();
		PassEvent(this);
	}

	public void FumbleBall() {
		LoseBall();
		SetRumble(1);
		SetRumble(0, 0.5f);
		FumbleEvent(this);
	}

	// MISC

	public float GetOrientationDifference(Player player) {
		Vector3 diff = normalizedOrientationDifference(normalizedOrientation(controller.Orientation.eulerAngles), normalizedOrientation(player.controller.Orientation.eulerAngles));
		Vector2 diff2 = new Vector2(diff.x, diff.y);

		return diff2.magnitude;
	}

	private void SetLED(Color color) {
		SetLED(color, null);
	}

	private void SetLED(Color color, float? delay) {
		if ( setLEDRoutine != null) 
			StopCoroutine(setLEDRoutine);

		if ( delay != null ) {
			setLEDRoutine = SetLEDCoroutine(color, (float)delay);
			StartCoroutine(setLEDRoutine);
		}
		else {
			controller.SetLED(color);
		}
	}

	private IEnumerator SetLEDCoroutine(Color color, float seconds) {
		yield return new WaitForSeconds(seconds); 
		SetLED(color); 
	}

	
	private void SetRumble(float rumble) {
		SetRumble(rumble, null);
	}
	
	private void SetRumble(float rumble, float? delay) {
		if ( setRumbleRoutine != null) 
			StopCoroutine(setRumbleRoutine);
		
		if ( delay != null ) {
			setRumbleRoutine = SetRumbleCoroutine(rumble, (float)delay);
			StartCoroutine(setRumbleRoutine);
		}
		else {
			controller.SetRumble(rumble);
		}
	}
	
	private IEnumerator SetRumbleCoroutine(float rumble, float seconds) {
		yield return new WaitForSeconds(seconds); 
		SetRumble(rumble); 
	}

	private void Flash(Color color1, Color color2) {
		if ( setLEDRoutine != null) 
			StopCoroutine(setLEDRoutine);

		SetLED(color1);
		setLEDRoutine = FlashCoroutine(color1, color2, 0.1f);
		StartCoroutine(setLEDRoutine);
	}

	private IEnumerator FlashCoroutine(Color color1, Color color2, float delay) {
		yield return new WaitForSeconds(delay);
		Flash(color2, color1);
	}
	



	private Vector3 normalizedOrientation(Vector3 o) {
		Vector3 orientation = o;
		orientation.x -= orientation.x > 180 ? 360 : 0;
		orientation.y -= orientation.y > 180 ? 360 : 0;
		orientation.z -= orientation.z > 180 ? 360 : 0;
		return orientation;
	}

	private Vector3 normalizedOrientationDifference(Vector3 normalizedOrientation1, Vector3 normalizedOrientation2) {
		Vector3 orientationDiff = normalizedOrientation1 - normalizedOrientation2;
		orientationDiff.x += (orientationDiff.x>180) ? -360 : (orientationDiff.x<-180) ? 360 : 0;
		orientationDiff.y += (orientationDiff.y>180) ? -360 : (orientationDiff.y<-180) ? 360 : 0;
		orientationDiff.z += (orientationDiff.z>180) ? -360 : (orientationDiff.z<-180) ? 360 : 0;
		return orientationDiff;
	}
}
