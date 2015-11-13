using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Crowd : MonoBehaviour {

	public AudioSource ambientSource;
	public AudioSource hypeSource;
	public AudioSource eventSource;

	public List<AudioClip> eventClips;

	public float intensifier;

	private float defaultAmbientVolume;
	private float defaultHypeVolume;
	private float defaultEventVolume;

	[EditorButtonAttribute()]
	public void Activate() {
		defaultAmbientVolume = ambientSource.volume;
		defaultHypeVolume = hypeSource.volume;
		defaultEventVolume = eventSource.volume;

		hypeSource.volume = 0;

		ambientSource.Play();
		hypeSource.Play();
	}

	[EditorButtonAttribute()]
	public void Deactivate() {
		ambientSource.volume = defaultAmbientVolume;
		hypeSource.volume = defaultHypeVolume;
		eventSource.volume = defaultEventVolume;

		ambientSource.Stop();
		hypeSource.Stop();
	}

	[EditorButtonAttribute()]
	public void Intensify() {
		if ( hypeSource.volume == 0 ) {
			hypeSource.volume = defaultHypeVolume;
		} else {
			hypeSource.volume *= intensifier;
		}
		eventSource.volume *= intensifier;
	}

	[EditorButtonAttribute()]
	public void Reset() {
		hypeSource.volume = 0;
		eventSource.volume = defaultEventVolume;
	}

	[EditorButtonAttribute()]
	public void Poke() {
		eventSource.PlayOneShot(eventClips[Random.Range(0, eventClips.Count)]);
	}
}
