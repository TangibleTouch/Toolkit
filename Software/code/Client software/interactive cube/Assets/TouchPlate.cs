using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TouchPlate : MonoBehaviour
{
    private Color initialColor;
    private Color hoverColor = Color.black;
    public int sensorNo = -1;
    public Button finishedSelectionButton;
    public Button recordGestureButton;
    public GameObject sensorSelection;
    public GameObject bluetooth;
    public Button detectGesturesButton;
    SensorSelection sensorSelectionScript;
    Bluetooth bluetoothScript;
    public Color sensorColor;
    public bool detect = false;
    public bool train = false;
    private bool selectedForTraining = false;


    void Start()
    {
        initialColor = gameObject.GetComponent<MeshRenderer>().material.GetColor("_Color");
        hoverColor.a = 0.90f;
        sensorSelectionScript = sensorSelection.GetComponent<SensorSelection>();
        bluetoothScript = bluetooth.GetComponent<Bluetooth>();
        finishedSelectionButton.onClick.AddListener(FinishSelection);
        recordGestureButton.onClick.AddListener(ToggleTraining);
        detectGesturesButton.onClick.AddListener(ToggleSelectedForTraining);
    }
    // Start is called before the first frame update
    void OnMouseOver()
    {
        if (sensorNo == -1)
            gameObject.GetComponent<MeshRenderer>().material.SetColor("_Color", hoverColor);
    }

    void OnMouseDown()
    {
        if(train)
            ToggleSelectedForTraining();
        else
            if (sensorSelectionScript.selectedSensorId != -1)
            {
                sensorNo = sensorSelectionScript.selectedSensorId;
                sensorColor = sensorSelectionScript.sensorColor[sensorNo];
                gameObject.GetComponent<MeshRenderer>().material.SetColor("_Color", sensorColor);
                if (!sensorSelectionScript.sensorLocations.ContainsKey(this.name))
                    sensorSelectionScript.sensorLocations.Add(this.name, sensorNo);
                else
                    sensorSelectionScript.sensorLocations[this.name] = sensorNo;
            }
    }

    void OnMouseExit()
    {
        if (train) { 
        if (!selectedForTraining)
            gameObject.GetComponent<MeshRenderer>().material.SetColor("_Color", initialColor);
        }
        else if (sensorNo == -1)
            gameObject.GetComponent<MeshRenderer>().material.SetColor("_Color", initialColor);
    }
    void FinishSelection()
    {
        detect = true;
        gameObject.GetComponent<MeshRenderer>().material.SetColor("_Color", initialColor);
    }

    public void ToggleTraining()
    {
        if (train)
            train = false;
        else
            train = true;
    }

    public void ToggleSelectedForTraining()
    {
        if (!train)
            train = true;
        if (selectedForTraining)
        {
            gameObject.GetComponent<MeshRenderer>().material.SetColor("_Color", initialColor);
            selectedForTraining = false;
            bluetoothScript.enabledSensors[sensorNo] = false;
        }
        else
        {
            gameObject.GetComponent<MeshRenderer>().material.SetColor("_Color", hoverColor);
            selectedForTraining = true;
            bluetoothScript.enabledSensors[sensorNo] = true;
        }
    }

    private void Update()
    {
        if(detect)
        {
            if (bluetoothScript.sensors[sensorNo].touched)
                gameObject.GetComponent<MeshRenderer>().material.SetColor("_Color", sensorColor);
            else if(selectedForTraining)
                gameObject.GetComponent<MeshRenderer>().material.SetColor("_Color", hoverColor);
            else
                gameObject.GetComponent<MeshRenderer>().material.SetColor("_Color", initialColor);
        }
    }

}
