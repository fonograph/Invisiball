using UnityEngine;
using UnityEngine.UI;
using System;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Audio;

public class Game : MonoBehaviour {

	public enum Phase { Connecting, Waiting, Playing, Ended };
	public enum PassMode { Simple, Imaginary, Button }

	public Player playerPrefab;
	public Text scoreText1;
	public Text scoreText2;
	public GameObject debugContainer;
	public List<GameObject> controllerObjs;

	public Color TeamColor1;
	public Color TeamColor2;
	public string TeamName1;
	public string TeamName2;
	public Color CycleColor;
	public Color BallColor;
	public Color ScoreColor;
	public int gameLength;
	public float accelTolerance;
	public float gyroTolerance;
	public float flatnessThreshold;
	public float scoreLength;
	public float scoreDecreaseLengthBonus;
	public float cycleStartLength;
	public float cycleIncreaseLength;
	public float catchMargin; // time you have to move after catching
	public float passMargin; // time you have after moving to start the pass (by releasing trigger)
	public float passLength; // time between releasing the trigger and the pass execution
	public float passAccelThreshold; // accel change threshold
	public float passCatchTimeout;
	public float deathLength;
	public float buttonDeathTimeout;
	public bool suppressFumble;
	public PassMode passMode;
	public bool enableAnnouncer;

	public AudioMixer audioMixer;

	public static Game Instance;

	private Board board;
	private Sounds sounds;
	private AudioSource audioSource;
	private AudioSource scoreAudioSource;
	private Crowd crowd;
	private Announcer announcer;

	private List<Player> players;
	private Dictionary<int, int> scores;

	private Phase phase;
	private bool isPractice;
	private float gameTime;
	private Player holdingPlayer;
	private Player lastHoldingPlayer;
	private List<Player> playerCycle;
	private int playerCycleIdx;
	private int playerCycleCount;
	private bool playerCycleStartedAfterPass; 
	private float currentScoreLength;
	private List<int> playedCountdownSounds;

	private IEnumerator ballCycleRoutine;
	private IEnumerator scoreRoutine;
	private IEnumerator passTimeoutRoutine;

	public Player HoldingPlayer {
		get { return holdingPlayer; }
	}

	void Awake() {
		Instance = this;
		board = FindObjectOfType<Board>();
		sounds = FindObjectOfType<Sounds>();
		audioSource = gameObject.GetComponent<AudioSource>();
		scoreAudioSource = gameObject.AddComponent<AudioSource>();
		crowd = FindObjectOfType<Crowd>();
		announcer = FindObjectOfType<Announcer>();
	}

	void Start() {
		#if !UNITY_EDITOR 
			Config config = Config.Load();
			gameLength = config.gameLength;
			accelTolerance = config.accelTolerance;
			gyroTolerance = config.gyroTolerance;
			scoreLength = config.scoreLength;
			scoreDecreaseLengthBonus = config.scoreDecreaseLengthBonus;
			cycleStartLength = config.cycleStartLength;
			cycleIncreaseLength = config.cycleIncreaseLength;
			catchMargin = config.catchMargin;
			passCatchTimeout = config.passCatchTimeout;
			deathLength = config.deathLength;
			suppressFumble = config.suppressFumble;
			enableAnnouncer = config.enableAnnouncer;
			
			audioMixer.SetFloat("announcerVolume", config.volumeAnnouncer);
			audioMixer.SetFloat("crowdVolume", config.volumeCrowd);
			audioMixer.SetFloat("sfxVolume", config.volumeSfx);
		#endif

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
				bool practice = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
				StartGame(practice);
			}

			// color switching
			foreach ( Player player in players ) {
				Color? setColor = null;
				string setName = null;
				if ( player.controller.GetButtonDown(PSMoveButton.Square) ) {
					setColor = new Color(1, 0, 0.5f);
					setName = "pink";
				}
				else if ( player.controller.GetButtonDown(PSMoveButton.Triangle) ) {
					setColor = new Color(0, 1, 0);
					setName = "green";
				}
				else if ( player.controller.GetButtonDown(PSMoveButton.Cross) ) {
					setColor = new Color(0, 0, 1);
					setName = "blue";
				}
				else if ( player.controller.GetButtonDown(PSMoveButton.Circle) ) {
					setColor = new Color(1, 0.5f, 0);
					setName = "orange";
				}

				if ( setColor != null ) {
					if ( player.team == 1 && !TeamColor2.Equals(setColor) )  {
						TeamColor1 = (Color)setColor;
						TeamName1 = (string)setName;
					}
					if ( player.team == 2 && !TeamColor1.Equals(setColor) ) {
						TeamColor2 = (Color)setColor;
						TeamName2 = (string)setName;
					}

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

			if ( !isPractice ) {
				gameTime -= Time.deltaTime;
			}

			// end game?
			if ( gameTime <= 0 ) {
				EndGame();
			}


			//update board
			board.seconds = gameTime;
			board.score1 = scores[1];
			board.score2 = scores[2];

			// countdown
			if ( gameTime < 60 && !playedCountdownSounds.Contains(60) ) {
				PlayCountdown(sounds.OneMinute, 60);
				announcer.PlayOneMinute(scores[1] > scores[2] ? TeamName1 : TeamName2);
			}
			if ( gameTime < 30 && !playedCountdownSounds.Contains(30) ) {
				PlayCountdown(sounds.ThirtySeconds, 30);
				announcer.PlayThirtySeconds(scores[1] > scores[2] ? TeamName1 : TeamName2);
			}
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
		if ( Input.GetKeyDown(KeyCode.P) ) {
			passMode = passMode==PassMode.Imaginary ? PassMode.Button : PassMode.Imaginary;
		}
		if ( Input.GetKeyDown(KeyCode.A) ) {
			enableAnnouncer = !enableAnnouncer;
			announcer.isEnabled = enableAnnouncer;
		}
	}

	void StartGame(bool practice) {
		phase = Phase.Playing;
		isPractice = practice;

		gameTime = gameLength;
		scores[1] = 0;
		scores[2] = 0;

		foreach ( Player player in players ) {
			player.inGame = true;
		}

		passTimeoutRoutine = null;

		playedCountdownSounds = new List<int>();

		playerCycle = GetRandomizedPlayers();
		
		playerCycleIdx = 0;
		playerCycleCount = 0;
		playerCycleStartedAfterPass = false;

		announcer.isEnabled = !isPractice && enableAnnouncer;

		announcer.PlayStart(delegate() {
			audioSource.PlayOneShot(sounds.GameStart);
			CycleBall(false);
		});

		if ( !isPractice ) {
			crowd.Activate();
		}
	}


	void StopGame() {
		phase = Phase.Waiting;

		if ( scoreRoutine != null ) StopCoroutine(scoreRoutine);
		if ( ballCycleRoutine != null ) StopCoroutine(ballCycleRoutine);
		if ( passTimeoutRoutine != null ) StopCoroutine(passTimeoutRoutine);

		gameTime = gameLength;
		scores[1] = 0;
		scores[2] = 0;

		foreach ( Player player in players ) {
			player.inGame = false;
			player.ResetLEDAndRumble();
		}

		crowd.Deactivate();
	}

	void EndGame() {
		phase = Phase.Ended;

		if ( scoreRoutine != null ) StopCoroutine(scoreRoutine);
		if ( ballCycleRoutine != null ) StopCoroutine(ballCycleRoutine);
		if ( passTimeoutRoutine != null ) StopCoroutine(passTimeoutRoutine);

		foreach ( Player player in players ) {
			player.inGame = false;

			int otherTeam = player.team == 1 ? 2 : 1;
			bool won = scores[player.team] > scores[otherTeam];
			player.SetEnding(won);
		}

		audioSource.PlayOneShot(sounds.GameEnd);

		crowd.Intensify();
		crowd.Poke();
		crowd.Poke();

		StartCoroutine(WaitAndPlayAnnouncerEnding(3));
		StartCoroutine(crowd.WaitAndDeactivate(8));
	}

	IEnumerator WaitAndPlayAnnouncerEnding(float seconds) {
		yield return new WaitForSeconds(seconds);
		announcer.PlayEnding(scores[1] > scores[2] ? TeamName1 : TeamName2);
	}

	void OnPlayerFumble(Player player) {
		if ( scoreRoutine != null ) StopCoroutine(scoreRoutine);

		// start ball cycle with randomized order

		playerCycle = GetRandomizedPlayers();
		playerCycle.Remove(holdingPlayer);

		playerCycleIdx = 0;
		playerCycleCount = 0;
		playerCycleStartedAfterPass = false;

		CycleBall(false);

		lastHoldingPlayer = holdingPlayer;
		holdingPlayer = null;

		audioSource.PlayOneShot(sounds.FumbleSound);

		crowd.Poke();
		announcer.PlayFumble();
	}

	void OnPlayerPass(Player player) {
		if ( scoreRoutine != null ) StopCoroutine(scoreRoutine);

		foreach ( Player p in GetRandomizedPlayers() ) {
			if ( p != holdingPlayer ) {
				if ( passMode == PassMode.Imaginary ) {
					if ( p.IsFlat && !p.IsDead ) {
						p.PassBallOn();
					}
				}
				else if ( passMode == PassMode.Button ) {
					if ( p.ButtonDown != null && p.ButtonDown == holdingPlayer.ButtonDown && !p.IsDead ) {
						p.PassBallOn();
					}
				}
				else if ( passMode == PassMode.Simple ) {
					if ( p.team == holdingPlayer.team ) {
						p.PassBallOn();
						break;
					}
				}
			} 
		}

		passTimeoutRoutine = WaitAndTimeoutPass(passCatchTimeout);
		StartCoroutine(passTimeoutRoutine);

		lastHoldingPlayer = holdingPlayer;
		holdingPlayer = null;

		audioSource.PlayOneShot(sounds.PassSound);
	}

	IEnumerator WaitAndTimeoutPass(float seconds) {
		yield return new WaitForSeconds(seconds);
		passTimeoutRoutine = null;

		foreach ( Player p in players ) {
			p.PassBallOff();
		}

		playerCycle = GetRandomizedPlayers();
		playerCycle.Remove(lastHoldingPlayer);
		
		playerCycleIdx = 0;
		playerCycleCount = 0;
		playerCycleStartedAfterPass = true;
		CycleBall(false);
	}

	void OnPlayerCatch(Player player) {
		bool fromPass = false;
		bool changedTeam = false;

		if ( ballCycleRoutine != null ) StopCoroutine(ballCycleRoutine);
		if ( passTimeoutRoutine != null ) {
			StopCoroutine(passTimeoutRoutine);
			passTimeoutRoutine = null;

			fromPass = true;
			foreach ( Player p in players ) {
				p.PassBallOff();
			}
		}

		holdingPlayer = player;

		if ( lastHoldingPlayer != null && lastHoldingPlayer.team == holdingPlayer.team ) {
			currentScoreLength -= scoreDecreaseLengthBonus;
			if ( currentScoreLength < 0.5 ) currentScoreLength = 0.5f;

			crowd.Intensify();
			crowd.Poke();
		} else {
			currentScoreLength = scoreLength;
			changedTeam = true;

			crowd.Reset();
			crowd.Poke();
		}

		Score(0);

		scoreAudioSource.pitch = 1f;
		if ( fromPass ) {
			audioSource.PlayOneShot(sounds.CatchPassSound);
			if ( changedTeam ) {
				announcer.PlayInterception();
			} else {
				announcer.PlayPass();
			}
		} 
		else {
			if ( playerCycleStartedAfterPass && changedTeam ) {
				announcer.PlayInterception(); // call it an "interception" when it changed team after a failed pass, even if it cycled first
			}
			audioSource.PlayOneShot(sounds.CatchFumbleSound);
		}
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

		bool everyoneDead = true;
		foreach ( Player p in playerCycle ) {
			everyoneDead = everyoneDead && p.IsDead;
		}

		if ( !everyoneDead ) {
			Player playerOn = null;
			while ( playerOn == null ) {
				if ( playerCycleIdx == playerCycle.Count ) {
					playerCycleIdx = 0;
					playerCycleCount++;
				}
				playerOn = playerCycle[playerCycleIdx];
				if ( playerOn.IsDead ) {
					playerOn = null;
					playerCycleIdx++;
				}
			}
		
			playerOn.CycleBallOn();
		} 

		float length = cycleStartLength + playerCycleCount*cycleIncreaseLength;

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

