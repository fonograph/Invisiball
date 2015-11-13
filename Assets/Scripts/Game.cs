using UnityEngine;
using UnityEngine.UI;
using System;
using System.Threading;
using System.Collections;
using System.Collections.Generic;

public class Game : MonoBehaviour {

	public enum Phase { Connecting, Waiting, Playing, Ended };

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

	private Board board;
	private Sounds sounds;
	private AudioSource audioSource;
	private AudioSource scoreAudioSource;
	private Crowd crowd;

	private List<Player> players;
	private Dictionary<int, int> scores;

	private Phase phase;

	private float gameTime;
	private Player holdingPlayer;
	private Player lastHoldingPlayer;
	private List<Player> playerCycle;
	private int playerCycleIdx;
	private int playerCycleCount;
	private bool playerCycleCurrentlyOnPass;
	private Dictionary<Player, float> playerCycleLengthOverride;
	private float currentScoreLength;
	private List<int> playedCountdownSounds;

	private IEnumerator ballCycleRoutine;
	private IEnumerator scoreRoutine;

	public Player HoldingPlayer {
		get { return holdingPlayer; }
	}

	void Awake() {
		Instance = this;
		board = FindObjectOfType<Board>();
		sounds = FindObjectOfType<Sounds>();
		audioSource = gameObject.AddComponent<AudioSource>();
		scoreAudioSource = gameObject.AddComponent<AudioSource>();
		crowd = FindObjectOfType<Crowd>();
	}

	void Start() {
		players = new List<Player>();

		scores = new Dictionary<int, int>();
		scores[1] = 0;
		scores[2] = 0;

		gameTime = gameLength;

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
			// start game
			if ( Input.GetKeyDown(KeyCode.Space) ) {
				StartGame();
			}

			// color switching
			foreach ( Player player in players ) {
				Color? setColor = null;
				if ( player.controller.GetButtonDown(PSMoveButton.Square) )
					setColor = new Color(1, 0, 0.5f);
				else if ( player.controller.GetButtonDown(PSMoveButton.Triangle) )
					setColor = new Color(0, 1, 0);
				else if ( player.controller.GetButtonDown(PSMoveButton.Cross) )
					setColor = new Color(0, 0, 1);
				else if ( player.controller.GetButtonDown(PSMoveButton.Circle) )
					setColor = new Color(1, 0.5f, 0);

				if ( setColor != null ) {
					if ( player.team == 1 && !TeamColor2.Equals(setColor) ) 
						TeamColor1 = (Color)setColor;
					if ( player.team == 2 && !TeamColor1.Equals(setColor) ) 
						TeamColor2 = (Color)setColor;

					foreach ( Player p in players ) 
						p.UpdateTeamColor();

					board.color1 = TeamColor1;
					board.color2 = TeamColor2;
				}
			}
		}

		else if ( phase == Phase.Playing ) {
			// stop game
			if ( Input.GetKeyDown(KeyCode.Space) ) {
				StopGame();
			}

			gameTime -= Time.deltaTime;

			// end game?
			if ( gameTime <= 0 ) {
				EndGame();
			}


			//update board
			board.seconds = gameTime;
			board.score1 = scores[1];
			board.score2 = scores[2];

			// countdown
			if ( gameTime < 60 && !playedCountdownSounds.Contains(60) ) PlayCountdown(sounds.OneMinute, 60);
			if ( gameTime < 30 && !playedCountdownSounds.Contains(30) ) PlayCountdown(sounds.ThirtySeconds, 30);
			if ( gameTime < 10 && !playedCountdownSounds.Contains(10) ) PlayCountdown(sounds.TenSeconds, 10);
			if ( gameTime < 9 && !playedCountdownSounds.Contains(9) ) PlayCountdown(sounds.NineSeconds, 9);
			if ( gameTime < 8 && !playedCountdownSounds.Contains(8) ) PlayCountdown(sounds.EightSeconds, 8);
			if ( gameTime < 7 && !playedCountdownSounds.Contains(7) ) PlayCountdown(sounds.SevenSeconds, 7);
			if ( gameTime < 6 && !playedCountdownSounds.Contains(6) ) PlayCountdown(sounds.SixSeconds, 6);
			if ( gameTime < 5 && !playedCountdownSounds.Contains(5) ) PlayCountdown(sounds.FiveSeconds, 5);
			if ( gameTime < 4 && !playedCountdownSounds.Contains(4) ) PlayCountdown(sounds.FourSeconds, 4);
			if ( gameTime < 3 && !playedCountdownSounds.Contains(3) ) PlayCountdown(sounds.ThreeSeconds, 3);
			if ( gameTime < 2 && !playedCountdownSounds.Contains(2) ) PlayCountdown(sounds.TwoSeconds, 2);
			if ( gameTime < 1 && !playedCountdownSounds.Contains(1) ) PlayCountdown(sounds.OneSeconds, 1);
		}

		else if ( phase == Phase.Ended ) {
			// stop game
			if ( Input.GetKeyDown(KeyCode.Space) ) {
				StopGame();
			}
		}

		if ( Input.GetKeyDown(KeyCode.D) ) {
			debugContainer.SetActive(!debugContainer.activeSelf);
		}
		if ( Input.GetKeyDown(KeyCode.R) ) {
			foreach ( Player p in players ) {
				if ( p.controller != null ) {
					p.controller.ResetOrientation();
				}
			}
		}
	}

	void StartGame() {
		phase = Phase.Playing;

		gameTime = gameLength;
		scores[1] = 0;
		scores[2] = 0;

		foreach ( Player player in players ) {
			player.inGame = true;
		}

		playedCountdownSounds = new List<int>();

		playerCycle = GetRandomizedPlayers();
		
		playerCycleIdx = 0;
		playerCycleCount = 0;
		CycleBall(false);

		audioSource.PlayOneShot(sounds.GameStart);

		crowd.Activate();
	}

	void StopGame() {
		phase = Phase.Waiting;

		if ( scoreRoutine != null ) StopCoroutine(scoreRoutine);
		if ( ballCycleRoutine != null ) StopCoroutine(ballCycleRoutine);

		gameTime = gameLength;
		scores[1] = 0;
		scores[2] = 0;

		foreach ( Player player in players ) {
			player.inGame = false;
			player.ResetLEDAndRumble();
		}

		crowd.Reset();
	}

	void EndGame() {
		phase = Phase.Ended;

		if ( scoreRoutine != null ) StopCoroutine(scoreRoutine);
		if ( ballCycleRoutine != null ) StopCoroutine(ballCycleRoutine);

		foreach ( Player player in players ) {
			player.inGame = false;

			int otherTeam = player.team == 1 ? 2 : 1;
			bool won = scores[player.team] > scores[otherTeam];
			player.SetEnding(won);
		}

		audioSource.PlayOneShot(sounds.GameEnd);

		crowd.Deactivate();
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

		audioSource.PlayOneShot(sounds.FumbleSound);

		crowd.Poke();
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

		audioSource.PlayOneShot(sounds.PassSound);
	}

	void OnPlayerCatch(Player player) {
		if ( ballCycleRoutine != null ) StopCoroutine(ballCycleRoutine);

		holdingPlayer = player;

		if ( lastHoldingPlayer != null && lastHoldingPlayer.team == holdingPlayer.team ) {
			currentScoreLength -= scoreDecreaseLengthBonus;
			if ( currentScoreLength < 0.5 ) currentScoreLength = 0.5f;

			crowd.Intensify();
			crowd.Poke();
		} else {
			currentScoreLength = scoreLength;

			crowd.Reset();
			crowd.Poke();
		}

		Score(0);

		scoreAudioSource.pitch = 1f;
		audioSource.PlayOneShot(playerCycleCurrentlyOnPass ? sounds.CatchPassSound : sounds.CatchFumbleSound);
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
			playerCycleCurrentlyOnPass = true;
		} else {
			playerCycleCurrentlyOnPass = false;
		}

		ballCycleRoutine = WaitAndCycleBall(length);
		StartCoroutine(ballCycleRoutine);

		if ( sounds.CycleSound != null )
			audioSource.PlayOneShot(sounds.CycleSound[playerCycleIdx]);
	}

	IEnumerator WaitAndCycleBall(float seconds) {
		yield return new WaitForSeconds(seconds);
		CycleBall(true);
	}

	void Score(int points) {
		scores[holdingPlayer.team] += points;

		if ( points > 0 ) {
			holdingPlayer.Score(points);

			scoreAudioSource.pitch += 0.1f;
			scoreAudioSource.PlayOneShot(sounds.ScoreSound);
		}

		scoreRoutine = WaitAndScore(currentScoreLength);
		StartCoroutine(scoreRoutine);
	}

	IEnumerator WaitAndScore(float seconds) {
		yield return new WaitForSeconds(seconds);
		Score(1);
	}

	void PlayCountdown(AudioClip sound, int id) {
		audioSource.PlayOneShot(sound);
		playedCountdownSounds.Add(id);
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
