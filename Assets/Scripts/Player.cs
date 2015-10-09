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
	private Vector3 startOrientation;


	public event Action<Player> FumbleEvent;
	public event Action<Player> PassEvent;
	public event Action<Player> CatchEvent;

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

			Vector3 orientationDiff = normalizedOrientationDifference(normalizedOrientation(controller.Orientation.eulerAngles), startOrientation);
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

		// DEBUG

//		if ( hasBall ) {
//			Debug.Log(controller.Orientation.eulerAngles);
//			Debug.Log((controller.Orientation.eulerAngles - startOrientation));
//			Debug.Log((controller.Orientation.eulerAngles - startOrientation).magnitude);
//		}
//		if ( Game.Instance.HoldingPlayer != null && Game.Instance.HoldingPlayer != this ) {
//			Vector2 direction = new Vector2(controller.Orientation.eulerAngles.x, controller.Orientation.eulerAngles.y);
//			Vector2 otherDirection = new Vector2(Game.Instance.HoldingPlayer.controller.Orientation.eulerAngles.x, Game.Instance.HoldingPlayer.controller.Orientation.eulerAngles.y);
//			//Debug.Log( (controller.Orientation.eulerAngles-Game.Instance.HoldingPlayer.controller.Orientation.eulerAngles).magnitude ) ;
//
//			if ( direction.x > 180 )
//				direction.x -= 360;
//			if ( direction.y > 180 )
//				direction.y -= 360;
//			if ( otherDirection.x > 180 ) 
//				otherDirection.x -= 360;
//			if ( otherDirection.y > 180 ) 
//				otherDirection.y -= 360;

			//Debug.Log( (direction-otherDirection).magnitude );
//			Debug.Log (direction);
//			Debug.Log (otherDirection);
//		}

		lastAccel = controller.Acceleration;
	}

	public void SetTeam(int theTeam) {
		team = theTeam;
		defaultColor = theTeam == 1 ? Game.Instance.TeamColor1 : Game.Instance.TeamColor2;

		controller.SetLED(defaultColor);
	}

	// STATE

	public void CycleBallOn() {
		controller.SetLED(Game.Instance.CycleColor);
		hasCycle = true;
		readyToCatch = false;
	}

	public void CycleBallOff() {
		controller.SetLED(defaultColor);
		hasCycle = false;
	}

	public void GainBall() {
		controller.SetLED(Game.Instance.BallColor);
		hasBall = true;
		hasBallStart = DateTime.Now;
		hasCycle = false;
		startedPass = false;
		startedFumble = false;
		startOrientation = normalizedOrientation(controller.Orientation.eulerAngles);
	}

	public void LoseBall() {
		controller.SetLED(defaultColor);
		hasBall = false;
	}

	public void Score(int points) {
		controller.SetLED(Game.Instance.ScoreColor);
		StartCoroutine(SetLEDDelayed(Game.Instance.BallColor, 0.2f));
	}

	// INTERNAL EVENTS

	public void CatchBall() {
		GainBall();
		CatchEvent(this);
	}

	public void PassBall() {
		LoseBall();
		PassEvent(this);
	}

	public void FumbleBall() {
		LoseBall();
		FumbleEvent(this);
	}

	// MISC

	public float GetOrientationDifference(Player player) {
		Vector3 diff = normalizedOrientationDifference(normalizedOrientation(controller.Orientation.eulerAngles), normalizedOrientation(player.controller.Orientation.eulerAngles));
		Vector2 diff2 = new Vector2(diff.x, diff.y);

		return diff2.magnitude;
	}

	private IEnumerator SetLEDDelayed(Color color, float seconds) {
		yield return new WaitForSeconds(seconds);
		controller.SetLED(color);
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
