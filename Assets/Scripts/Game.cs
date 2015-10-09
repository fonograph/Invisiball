using UnityEngine;
using UnityEngine.UI;
using System.Threading;
using System.Collections;
using System.Collections.Generic;

public class Game : MonoBehaviour {

	public enum Phase { Connecting, Waiting, Playing };

	public Player playerPrefab;
	public Text scoreText1;
	public Text scoreText2;
	public List<GameObject> controllerObjs;

	public Color TeamColor1;
	public Color TeamColor2;
	public Color CycleColor;
	public Color BallColor;
	public Color ScoreColor;
	public AudioClip CatchSound;
	public AudioClip FumbleSound;
	public AudioClip PassSound;
	public AudioClip ScoreSound;
	public float accelTolerance;
	public float gyroTolerance;
	public float cycleStartLength;
	public float cycleIncreaseLength;
	public float cyclePassLength;
	public float catchMargin;
	public float passMargin;
	public float passLength;
	public bool suppressFumble;

	public static Game Instance;

	private AudioSource audioSource;

	private List<Player> players;
	private Dictionary<int, int> scores;
	private Dictionary<int, Text> scoreTexts;

	private Phase phase;

	private Player holdingPlayer;
	private List<Player> playerCycle;
	private int playerCycleIdx;
	private int playerCycleCount;

	private IEnumerator ballCycleRoutine;
	private IEnumerator scoreRoutine;

	public Player HoldingPlayer {
		get { return holdingPlayer; }
	}

	void Awake() {
		Instance = this;
		audioSource = GetComponent<AudioSource>();
	}

	void Start() {
		players = new List<Player>();

		scores = new Dictionary<int, int>();
		scores[1] = 0;
		scores[2] = 0;

		scoreTexts = new Dictionary<int, Text>();
		scoreTexts[1] = scoreText1;
		scoreTexts[2] = scoreText2;

		scoreText1.text = "0";
		scoreText2.text = "0";

		int count = UniMoveController.GetNumConnected();
		Debug.Log("Controllers connected: " + count);

		for (int i = 0; i < count; i++)
		{
			Player player = Instantiate(playerPrefab);
			player.FumbleEvent += OnPlayerFumble;
			player.CatchEvent += OnPlayerCatch;
			player.PassEvent += OnPlayerPass;
			players.Add(player);
		}

		phase = Phase.Connecting;
	}

	// Update is called once per frame
	void Update () {
		if ( phase == Phase.Connecting ) {
			bool allConnected = true;
			for ( int i=0; i<players.Count; i++ ) {
				if ( players[i].controller == null ) {
					UniMoveController controller = players[i].gameObject.AddComponent<UniMoveController>();
					if ( controller.Init(i) ) {
						controller.SetLED(Color.white);
						controller.InitOrientation();
						controller.ResetOrientation();
						players[i].Init(controller, controllerObjs[i]);
					} else {
						Destroy(controller);
						allConnected = false;
					}
				} 
			}
			if ( allConnected ) {
				phase = Phase.Waiting;

				int team = 0;
				foreach ( Player player in players ) {
					player.SetTeam( team%2 + 1 );
					team++;
				}
			}
		}

		else if ( phase == Phase.Waiting ) {
			if ( Input.GetKeyDown(KeyCode.Space) ) {
				StartGame();
			}
		}
	}

	void StartGame() {
		foreach ( Player player in players ) {
			player.inGame = true;
		}

		playerCycle = GetRandomizedPlayers();
		
		playerCycleIdx = 0;
		playerCycleCount = 0;
		CycleBall(false, null);
	}

	void OnPlayerFumble(Player player) {
		StopCoroutine(scoreRoutine);

		// start ball cycle with randomized order

		playerCycle = GetRandomizedPlayers();
		playerCycle.Remove(holdingPlayer);

		playerCycleIdx = 0;
		playerCycleCount = 0;
		CycleBall(false, null);

		holdingPlayer = null;

		audioSource.PlayOneShot(FumbleSound);
	}

	void OnPlayerPass(Player player) {
		StopCoroutine(scoreRoutine);

		// figure out who's closest in orientation, and start a cycle with that player
		Player closest = null;
		foreach ( Player p in players ) {
			if ( p != holdingPlayer ) {
				if ( closest == null || holdingPlayer.GetOrientationDifference(p) < holdingPlayer.GetOrientationDifference(closest) ) {
					closest = p;
				}
			}
		}

		playerCycle = GetRandomizedPlayers();
		playerCycle.Remove(holdingPlayer);

		playerCycle.Remove(closest);
		playerCycle.Insert(0, closest);

		playerCycleIdx = 0; 
		playerCycleCount = 0;
		CycleBall(false, cyclePassLength);

		holdingPlayer = null;

		audioSource.PlayOneShot(PassSound);
	}

	void OnPlayerCatch(Player player) {
		StopCoroutine(ballCycleRoutine);
		holdingPlayer = player;

		Score(0);

		audioSource.PlayOneShot(CatchSound);
	}

	void CycleBall(bool advance, float? overrideLength) {
		if ( advance ) {
			Player playerOff = playerCycle[playerCycleIdx];
			playerOff.CycleBallOff();

			playerCycleIdx++;
			if ( playerCycleIdx == playerCycle.Count ) {
				playerCycleIdx = 0;
				playerCycleCount++;
			}
		}

		Player playerOn = playerCycle[playerCycleIdx];
		playerOn.CycleBallOn();

		float? length = overrideLength ?? cycleStartLength + playerCycleCount*cycleIncreaseLength;
		ballCycleRoutine = WaitAndCycleBall((float)length);
		StartCoroutine(ballCycleRoutine);
	}

	IEnumerator WaitAndCycleBall(float seconds) {
		yield return new WaitForSeconds(seconds);
		CycleBall(true, null);
	}

	void Score(int points) {
		scores[holdingPlayer.team] += points;
		scoreTexts[holdingPlayer.team].text = scores[holdingPlayer.team].ToString();

		if ( points > 0 ) {
			holdingPlayer.Score(points);
			audioSource.PlayOneShot(ScoreSound);
		}

		scoreRoutine = WaitAndScore(2);
		StartCoroutine(scoreRoutine);
	}

	IEnumerator WaitAndScore(float seconds) {
		yield return new WaitForSeconds(seconds);
		Score(1);
	}


	List<Player> GetRandomizedPlayers() {
		List<Player> list = new List<Player>(players);
		
		for (int i = list.Count; i > 1; i--) {
			int pos = Random.Range(0, i-1);
			var x = list[i - 1];
			list[i - 1] = list[pos];
			list[pos] = x;
		}

		return list;
	}

}
