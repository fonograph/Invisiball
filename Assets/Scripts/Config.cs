using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Xml.Serialization;


public class Config {

	public static Config Load() {
		XmlSerializer serial = new XmlSerializer(typeof(Config));
        Stream reader = new FileStream(Application.dataPath + "/StreamingAssets/config.xml", FileMode.Open);
        return (Config)serial.Deserialize(reader);
	}

	public int gameLength;
	public float accelTolerance;
	public float gyroTolerance;
	public float scoreLength;
	public float scoreDecreaseLengthBonus;
	public float cycleStartLength;
	public float cycleIncreaseLength;
	public float catchMargin;
	public float passCatchTimeout;
	public float deathLength;
	public bool suppressFumble;
	public bool enableAnnouncer;
	public float volumeAnnouncer;
	public float volumeCrowd;
	public float volumeSfx;

}
