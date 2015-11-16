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
	private bool hasPass;
	private bool readyToCatch;
	private bool startedPass;
	private bool startedFumble;
	private Vector3 lastAccel = Vector3.zero;
	private Vector3 registeredOrientation;
	private bool dead;
	private PSMoveButton? buttonDown;
	private DateTime? buttonDownStart;
	private IEnumerator setLEDRoutine;
	private IEnumerator setRumbleRoutine;

	public event Action<Player> FumbleEvent;
	public event Action<Player> PassEvent;
	public event Action<Player> CatchEvent;

	public bool IsFlat {
		get { return Mathf.Abs(controllerObj.transform.forward.z) < Game.Instance.flatnessThreshold; }
	}
	public bool IsDead {
		get { return dead; }
	}
	public PSMoveButton? ButtonDown {
		get { return buttonDown; }
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
		
		// Button presses
		if ( controller.GetButtonDown(PSMoveButton.Circle) ) {
			buttonDown = PSMoveButton.Circle;
			buttonDownStart = DateTime.Now;
		}
		if ( controller.GetButtonDown(PSMoveButton.Square) ) {
			buttonDown = PSMoveButton.Square;
			buttonDownStart = DateTime.Now;
		}
		if ( controller.GetButtonDown(PSMoveButton.Cross) ) {
			buttonDown = PSMoveButton.Cross;
			buttonDownStart = DateTime.Now;
		}
		if ( controller.GetButtonDown(PSMoveButton.Triangle) ) {
			buttonDown = PSMoveButton.Triangle;
			buttonDownStart = DateTime.Now;
		}
		if ( controller.GetButtonUp(PSMoveButton.Circle) && buttonDown == PSMoveButton.Circle ) {
			buttonDown = null;
			buttonDownStart = null;
		}
		if ( controller.GetButtonUp(PSMoveButton.Square) && buttonDown == PSMoveButton.Square ) {
			buttonDown = null;
			buttonDownStart = null;
		}
		if ( controller.GetButtonUp(PSMoveButton.Cross) && buttonDown == PSMoveButton.Cross ) {
			buttonDown = null;
			buttonDownStart = null;
		}
		if ( controller.GetButtonUp(PSMoveButton.Triangle) && buttonDown == PSMoveButton.Triangle ) {
			buttonDown = null;
			buttonDownStart = null;
		}

		if ( !inGame ) 
			return;

		// CATCHING
		// you need to press the trigger WHILE you have the cycle
		if ( hasCycle || hasPass ) {
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
			if ( Game.Instance.passMode == Game.PassMode.Button && buttonDown == null ) {
				// no button in button mode equals a fumble
				FumbleBall();
				CancelInvoke("FumbleBall");
			}
			else {
				startedPass = true;
				CancelInvoke("FumbleBall");
				Invoke("FumbleBall", Game.Instance.passLength); // time out on completing the pass
			}
		}
		else if ( hasBall && startedPass ) {
			// sufficient thrust
			if ( (controller.Acceleration-lastAccel).magnitude > Game.Instance.passAccelThreshold && IsFlat ) {
				CancelInvoke("FumbleBall");
				PassBall();
			}
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

		// NO BALL DEATHS
		if ( !hasBall ) {
			if ( Game.Instance.passMode == Game.PassMode.Button ) {
				// if you don't have the ball and you hold down a button for a while kill you
				if ( buttonDown != null && DateTime.Now.Subtract((DateTime)buttonDownStart).TotalSeconds > Game.Instance.buttonDeathTimeout ) {
					Die();
				}
			}
			// if you try to catch while you can't, kill you
			if ( !hasCycle && !hasPass && controller.Trigger > 0 ) {
				Die ();
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

	public void PassBallOn() {
		if ( Game.Instance.passMode == Game.PassMode.Button ) {
			SetLED(Game.Instance.CycleColor);
			SetRumble(0.3f);
			SetRumble(0, 0.3f);
		}
		hasPass = true;
		readyToCatch = false;
	}

	public void PassBallOff() {
		if ( Game.Instance.passMode == Game.PassMode.Button ) {
			SetLED(defaultColor);
		}
		hasPass = false;
	}

	public void GainBall() {
//		SetLED(Game.Instance.BallColor);
		Flash(Game.Instance.BallColor, defaultColor);
		hasBall = true;
		hasBallStart = DateTime.Now;
		hasCycle = false;
		hasPass = false;
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
		SetRumble(0.5f);
		SetRumble(0, 0.3f);
		CatchEvent(this);
		GainBall();
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

	public void Die() {
		dead = true;
		SetLED(Color.black);
		StartCoroutine(Revive());
	}
	
	public IEnumerator Revive() {
		yield return new WaitForSeconds(Game.Instance.deathLength);
		SetLED(defaultColor);
		dead = false;
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
