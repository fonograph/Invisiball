using UnityEngine;
using UnityEngine.UI;
using System;
using System.Threading;
using System.Collections;
using System.Collections.Generic;

public class Game : MonoBehaviour {

	public enum Phase { Connecting, Waiting, Playing };

	public Player playerPrefab;
	public Text scoreText1;
	public Text scoreText2;
	public GameObject debugContainer;
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
	public int gameLength;
	public float accelTolerance;
	public float gyroTolerance;
	public float orientationSamenessThreshold;
	public float scoreLength;
	public float scoreDecreaseLengthBonus;
	public float cycleStartLength;
	public float cycleIncreaseLength;
	public float cyclePassLength;
	public float catchMargin;
	public float passMargin;
	public float passLength;
	public bool suppressFumble;

	public static Game Instance;

	private AudioSource audioSource;
	private Board board;

	private List<Player> players;
	private Dictionary<int, int> scores;

	private Phase phase;

	private float gameTime;
	private Player holdingPlayer;
	private Player lastHoldingPlayer;
	private List<Player> playerCycle;
	private int playerCycleIdx;
	private int playerCycleCount;
	private Dictionary<Player, float> playerCycleLengthOverride;
	private float currentScoreLength;

	private IEnumerator ballCycleRoutine;
	private IEnumerator scoreRoutine;

	public Player HoldingPlayer {
		get { return holdingPlayer; }
	}

	void Awake() {
		Instance = this;
		audioSource = GetComponent<AudioSource>();
		board = FindObjectOfType<Board>();
	}

	void Start() {
		players = new List<Player>();

		scores = new Dictionary<int, int>();
		scores[1] = 0;
		scores[2] = 0;

		board.color1 = TeamColor1;
		board.color2 = TeamColor2;

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

		else if ( phase == Phase.Playing ) {
			gameTime -= Time.deltaTime;
			board.seconds = gameTime;
			board.score1 = scores[0];
			board.score2 = scores[2];
		}

		if ( Input.GetKeyDown(KeyCode.D) ) {
			debugContainer.SetActive(!debugContainer.activeSelf);
		}
	}

	void StartGame() {
		gameTime = gameLength;

		foreach ( Player player in players ) {
			player.inGame = true;
		}

		playerCycle = GetRandomizedPlayers();
		
		playerCycleIdx = 0;
		playerCycleCount = 0;
		CycleBall(false);
	}

	void OnPlayerFumble(Player player) {
		if ( scoreRoutine != null ) StopCoroutine(scoreRoutine);

		// start ball cycle with randomized order

		playerCycle = GetRandomizedPlayers();
		playerCycle.Remove(holdingPlayer);

		playerCycleLengthOverride = null;

		playerCycleIdx = 0;
		playerCycleCount = 0;
		CycleBall(false);

		lastHoldingPlayer = holdingPlayer;
		holdingPlayer = null;

		audioSource.PlayOneShot(FumbleSound);
	}

	void OnPlayerPass(Player player) {
		if ( scoreRoutine != null ) StopCoroutine(scoreRoutine);

		playerCycleLengthOverride = new Dictionary<Player, float>();

		List<Player> passablePlayers = new List<Player>();
		foreach ( Player p in players ) {
			if ( p != holdingPlayer ) {
				if ( holdingPlayer.GetOrientationDifference(p) < orientationSamenessThreshold ) {
					passablePlayers.Add(p);
				}
//				if ( closest == null || holdingPlayer.GetOrientationDifference(p) < holdingPlayer.GetOrientationDifference(closest) ) {
//					closest = p;
//				}
			}
		}

		passablePlayers.Sort( (p1,p2) => (int)(p2.TimeInOrientation - p1.TimeInOrientation) ); // if P2 has more time, it "less than" P1 meaning it comes first

		playerCycle = GetRandomizedPlayers();
		playerCycle.Remove(holdingPlayer);

		for ( int i=passablePlayers.Count-1; i>=0; i-- ) {
			Player p = passablePlayers[i];
			playerCycle.Remove(p);
			playerCycle.Insert(0, p);
			playerCycleLengthOverride[p] = cyclePassLength;
		}

		playerCycleIdx = 0; 
		playerCycleCount = 0;
		CycleBall(false);

		lastHoldingPlayer = holdingPlayer;
		holdingPlayer = null;

		audioSource.PlayOneShot(PassSound);
	}

	void OnPlayerCatch(Player player) {
		if ( ballCycleRoutine != null ) StopCoroutine(ballCycleRoutine);

		holdingPlayer = player;

		if ( lastHoldingPlayer != null && lastHoldingPlayer.team == holdingPlayer.team ) {
			currentScoreLength -= scoreDecreaseLengthBonus;
		} else {
			currentScoreLength = scoreLength;
		}

		Score(0);

		audioSource.PlayOneShot(CatchSound);
	}

	void CycleBall(bool advance) {
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

		float length = cycleStartLength + playerCycleCount*cycleIncreaseLength;

		if ( playerCycleLengthOverride != null && playerCycleLengthOverride.ContainsKey(playerOn) ) {
			length = playerCycleLengthOverride[playerOn];
			playerCycleLengthOverride.Remove(playerOn);
		}

		ballCycleRoutine = WaitAndCycleBall(length);
		StartCoroutine(ballCycleRoutine);
	}

	IEnumerator WaitAndCycleBall(float seconds) {
		yield return new WaitForSeconds(seconds);
		CycleBall(true);
	}

	void Score(int points) {
		scores[holdingPlayer.team] += points;

		if ( points > 0 ) {
			holdingPlayer.Score(points);
			audioSource.PlayOneShot(ScoreSound);
		}

		scoreRoutine = WaitAndScore(currentScoreLength);
		StartCoroutine(scoreRoutine);
	}

	IEnumerator WaitAndScore(float seconds) {
		yield return new WaitForSeconds(seconds);
		Score(1);
	}


	List<Player> GetRandomizedPlayers() {
		List<Player> list = new List<Player>(players);
		
		for (int i = list.Count; i > 1; i--) {
			int pos = UnityEngine.Random.Range(0, i-1);
			var x = list[i - 1];
			list[i - 1] = list[pos];
			list[pos] = x;
		}

		return list;
	}

}
