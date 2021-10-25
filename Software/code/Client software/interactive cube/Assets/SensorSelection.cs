using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using UnityEngine.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

public class SensorSelection : MonoBehaviour
{

    public GameObject sensorSelectionsProto;
    public GameObject sensorSelectionPanel;
    private GameObject[] choices;
    private bool started = false;
    public int selectedSensorId = -1;

    public string sensorLocationsPath = "sensorLocations.txt";

    public Dictionary<string, int> sensorLocations = new Dictionary<string, int>();
    public List<string> sensorLocationsList = new List<string>();
    Transform sensorSelectionsRoot;

    public Color[] sensorColor;


    public GameObject bluetooth;
    Bluetooth bluetoothScript;
    // Start is called before the first frame update
    void Start()
    {

    }

    public void StartSelection()
    {
        sensorSelectionPanel.gameObject.SetActive(true);
        sensorSelectionsRoot = sensorSelectionsProto.transform.parent;
        sensorSelectionsProto.gameObject.SetActive(false);
        bluetoothScript = bluetooth.GetComponent<Bluetooth>();

        choices = new GameObject[12];
        sensorColor = new Color[12];
        for (int i = 0; i < bluetoothScript.sensors.Length; i++)
        {
            sensorColor[i] = new Color(
                UnityEngine.Random.Range(0f, 1f),
                UnityEngine.Random.Range(0f, 1f),
                UnityEngine.Random.Range(0f, 1f)
            );
            GameObject g = Instantiate(sensorSelectionsProto, sensorSelectionsRoot);
            g.name = i.ToString();
            g.transform.GetChild(0).GetComponent<Text>().text = "Sensor " + i;
            g.transform.GetChild(0).GetComponent<Text>().color = sensorColor[i];
            g.gameObject.SetActive(true);
        }
        started = true;
    }

    public void SelectSensor(GameObject data)
    {
        for (int i = 1; i < sensorSelectionsRoot.transform.childCount; i++)
        {
            GameObject child = sensorSelectionsRoot.transform.GetChild(i).gameObject;
            child.transform.GetChild(0).GetComponent<Text>().color = child == data ? Color.gray :
                sensorColor[i-1];
        }
        selectedSensorId = Int32.Parse(data.name);
        Debug.Log(selectedSensorId);
    }

    public void FinishSelection()
    {
        Save();
        started = false;
        selectedSensorId = -1;
        sensorSelectionPanel.gameObject.SetActive(false);
    }


    private void Save()
    {
        FileStream fs = new FileStream(sensorLocationsPath, FileMode.Create);
        BinaryFormatter bf = new BinaryFormatter();
        bf.Serialize(fs, sensorLocations);
        fs.Close();
    }

    public void Load()
    {
        using (Stream stream = File.Open(sensorLocationsPath, FileMode.Open))
        {
            var bformatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();

            sensorLocations = (Dictionary<string,int>)bformatter.Deserialize(stream);
        }

        int index = 0;
        foreach (KeyValuePair<string, int> entry in sensorLocations)
        {
            GameObject obj = GameObject.Find(entry.Key);
            TouchPlate touchPlate = obj.GetComponent<TouchPlate>();
            touchPlate.sensorNo = entry.Value;
            touchPlate.detect = true;
            touchPlate.sensorColor = sensorColor[index];
            index++;
        }
        started = false;
        selectedSensorId = -1;
        sensorSelectionPanel.gameObject.SetActive(false);
    }


    // Update is called once per frame
    void Update()
    {
        if(started)
            for (int i = 1; i < sensorSelectionsRoot.transform.childCount; i++)
            {
                GameObject child = sensorSelectionsRoot.transform.GetChild(i).gameObject;
                Color prevColor = child.transform.GetChild(0).GetComponent<Text>().color;
                if (bluetoothScript.sensors[i - 1].touched)
                    child.transform.GetChild(0).GetComponent<Text>().color = Color.red;
                else if (i == selectedSensorId + 1)
                    child.transform.GetChild(0).GetComponent<Text>().color = Color.gray;
                else
                    child.transform.GetChild(0).GetComponent<Text>().color = sensorColor[i-1];
            }
    }
}
