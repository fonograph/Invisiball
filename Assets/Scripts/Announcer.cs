using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class Announcer : MonoBehaviour {

	public float fumbleFrequency;
	public float passFrequency;
	public float interceptFrequency;
	public bool isEnabled;

	public List<AudioClip> startSounds;
	public List<AudioClip> oneMinuteSounds;
	public List<AudioClip> thirtySecondsSounds;
	public List<AudioClip> pinkWinningSounds;
	public List<AudioClip> greenWinningSounds;
	public List<AudioClip> blueWinningSounds;
	public List<AudioClip> orangeWinningSounds;
	public List<AudioClip> pinkWonSounds;
	public List<AudioClip> greenWonSounds;
	public List<AudioClip> blueWonSounds;
	public List<AudioClip> orangeWonSounds;
	public List<AudioClip> passSounds;
	public List<AudioClip> interceptionSounds;
	public List<AudioClip> fumbleSounds;

	private int startCount = 0;
	private int oneMinuteCount = 0;
	private int thirtySecondsCount = 0;
	private int pinkWinningCount = 0;
	private int greenWinningCount = 0;
	private int blueWinningCount = 0;
	private int orangeWinningCount = 0;
	private int pinkWonCount = 0;
	private int greenWonCount = 0;
	private int blueWonCount = 0;
	private int orangeWonCount = 0;
	private int passCount = 0;
	private int fumbleCount = 0;
	private int interceptionCount = 0;

	private AudioSource audioSource;

	void Awake() {
		audioSource = GetComponent<AudioSource>();
	}

	public void PlayStart(Action onComplete) {
		AudioClip sound = PlaySound(startSounds, ref startCount);
		StartCoroutine(CallActionAfterClipComplete(onComplete, sound));
	}

	public void PlayOneMinute(string winningTeam) {
		AudioClip sound = PlaySound(oneMinuteSounds, ref oneMinuteCount);
		StartCoroutine(CallActionAfterClipComplete(delegate() {
			if ( winningTeam == "pink" ) {
				PlaySound(pinkWinningSounds, ref pinkWinningCount);
			}
			if ( winningTeam == "green" ) {
				PlaySound(greenWinningSounds, ref greenWinningCount);
			}
			if ( winningTeam == "blue" ) {
				PlaySound(blueWinningSounds, ref blueWinningCount);
			}
			if ( winningTeam == "orange" ) {
				PlaySound(orangeWinningSounds, ref orangeWinningCount);
			}
		}, sound));
	}

	public void PlayThirtySeconds(string winningTeam) {
		AudioClip sound = PlaySound(thirtySecondsSounds, ref thirtySecondsCount);
		StartCoroutine(CallActionAfterClipComplete(delegate() {
			if ( winningTeam == "pink" ) {
				PlaySound(pinkWinningSounds, ref pinkWinningCount);
			}
			if ( winningTeam == "green" ) {
				PlaySound(greenWinningSounds, ref greenWinningCount);
			}
			if ( winningTeam == "blue" ) {
				PlaySound(blueWinningSounds, ref blueWinningCount);
			}
			if ( winningTeam == "orange" ) {
				PlaySound(orangeWinningSounds, ref orangeWinningCount);
			}
		}, sound));
	}

	public void PlayEnding(string winningTeam) {
		if ( winningTeam == "pink" ) {
			PlaySound(pinkWonSounds, ref pinkWonCount);
		}
		if ( winningTeam == "green" ) {
			PlaySound(greenWonSounds, ref greenWonCount);
		}
		if ( winningTeam == "blue" ) {
			PlaySound(blueWonSounds, ref blueWonCount);
		}
		if ( winningTeam == "orange" ) {
			PlaySound(orangeWonSounds, ref orangeWonCount);
		}
	}

	public void PlayFumble() {
		if ( UnityEngine.Random.value < fumbleFrequency ) {
			PlaySound(fumbleSounds, ref fumbleCount);
		}
	}

	public void PlayPass() {
		if ( UnityEngine.Random.value < passFrequency ) {
			PlaySound(passSounds, ref passCount);
		}
	}

	public void PlayInterception() {
		if ( UnityEngine.Random.value < interceptFrequency ) {
			PlaySound(interceptionSounds, ref interceptionCount);
		}
	}

	private AudioClip PlaySound(List<AudioClip> sounds, ref int soundCount) {
		if ( !isEnabled ) {
			return null;
		}
		AudioClip sound = sounds[soundCount % sounds.Count];
		audioSource.PlayOneShot(sound);
		soundCount++;
		return sound;
	}

	private IEnumerator CallActionAfterClipComplete(Action action, AudioClip clip) {
		float length = clip != null ? clip.length-1 : 0;
		yield return new WaitForSeconds(length); 
		action();
	}

}
