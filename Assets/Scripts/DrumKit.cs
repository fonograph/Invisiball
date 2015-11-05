using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class DrumKit : MonoBehaviour {


	private List<UniMoveController> controllers = new List<UniMoveController>();
	private bool allConnected;
	private AudioSource aud;
	private DateTime lastShake;

	// Use this for initialization
	void Start () {
		aud = GetComponent<AudioSource>();
		aud.Play();

		for ( int i=0; i<UniMoveController.GetNumConnected(); i++ ) { 
			controllers.Add(null);
		}
	}
	
	// Update is called once per frame
	void Update () {
		if ( !allConnected ) {
			allConnected = true;
			for ( int i=0; i<UniMoveController.GetNumConnected(); i++ ) {
				if ( controllers[i] == null ) {
					UniMoveController controller = gameObject.AddComponent<UniMoveController>();
					if ( controller.Init(i) ) {
						controller.SetLED(Color.white);
						controller.InitOrientation();
						controller.ResetOrientation();
						controllers[i] = controller;
					} else {
						Destroy(controller);
						allConnected = false;
					}
				} 
			}
		}	

		else {
			foreach ( UniMoveController controller in controllers ) {
				Debug.Log (controller.Acceleration.magnitude);
				if ( controller != null && controller.Acceleration.magnitude > 2 ) {
					aud.UnPause();
					controller.SetLED(new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value));
					lastShake = DateTime.Now;
				}
			}
		}

		if ( DateTime.Now.Subtract(lastShake).TotalSeconds > 0.2 ) {
			aud.Pause();
		}
	}
}
