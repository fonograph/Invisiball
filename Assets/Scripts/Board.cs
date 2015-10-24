using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class Board : MonoBehaviour {

	public float seconds;
	public int score1;
	public int score2;
	public Color color1;
	public Color color2;

	public Text text1;
	public Image border1;
	public GameObject spinner1;
	public Image circleA1;
	public Image circleB1;

	public Text text2;
	public Image border2;
	public GameObject spinner2;
	public Image circleA2;
	public Image circleB2;

	public Text timeText;

	// Use this for initialization
	void Start () {
		iTween.RotateBy(spinner1, iTween.Hash("amount", new Vector3(0, 0, 1), "time", 3.0f, "looptype", "loop", "easetype", "linear"));
		iTween.RotateBy(spinner2, iTween.Hash("amount", new Vector3(0, 0, 1), "time", 3.0f, "looptype", "loop", "easetype", "linear"));

		score1 = 0;
		score2 = 0;
	}

	// Update is called once per frame
	void Update () {

		// colors

		Color lightColor1 = color1;
		lightColor1.r = lightColor1.r/2 + 0.5f;
		lightColor1.g = lightColor1.g/2 + 0.5f;
		lightColor1.b = lightColor1.b/2 + 0.5f;
		
		text1.GetComponent<Outline>().effectColor = lightColor1;
		border1.color = color1;
		circleA1.color = color1;
		circleB1.color = lightColor1;

		Color lightColor2 = color2;
		lightColor2.r = lightColor2.r/2 + 0.5f;
		lightColor2.g = lightColor2.g/2 + 0.5f;
		lightColor2.b = lightColor2.b/2 + 0.5f;
		
		text2.GetComponent<Outline>().effectColor = lightColor2;
		border2.color = color2;
		circleA2.color = color2;
		circleB2.color = lightColor2;

		// scores

		text1.text = score1.ToString();
		float scale1 = 1 + ( (float)score1 / (score1+10) )*5;
		circleA1.rectTransform.localScale = new Vector3(scale1, scale1, 1);
		circleB1.rectTransform.localScale = new Vector3(scale1, scale1, 1);

		text2.text = score2.ToString();
		float scale2 = 1 + ( (float)score2 / (score2+10) )*5;
		circleA2.rectTransform.localScale = new Vector3(scale2, scale2, 1);
		circleB2.rectTransform.localScale = new Vector3(scale2, scale2, 1);

		// time
		int m = Mathf.CeilToInt(seconds) / 60;
		int s = Mathf.CeilToInt(seconds) % 60;
		timeText.text = m.ToString() + ":" +s.ToString().PadLeft(2, '0');
	}
	

}
