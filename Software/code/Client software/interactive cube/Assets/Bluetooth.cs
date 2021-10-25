using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System;



public class Bluetooth : MonoBehaviour
{
    public Text statusText;
    //public Button serviceScanButton;
    public Text deviceScanButtonText;
    public GameObject bluetoothPanel;

    public Button finished;
    public Text subscribeText;
    public Button resetButton;

    public Text errorText;

    public bool isScanningDevices = false;
    public bool isScanningServices = false;
    public bool isScanningCharacteristics = false;
    public bool isSubscribed = false;
    private bool isSetupFisnished = false;
    private bool isRecording = false;
    public static int SENSOR_AMOUNT = 12;

    public GameObject deviceScanResultProto;

    Transform scanResultRoot;
    public string selectedDeviceId;

    // UART IDs
    private static readonly string UARTServiceId = "{6e400001-b5a3-f393-e0a9-e50e24dcca9e}";
    private static readonly string UARTReadCharacteristic = "{6e400003-b5a3-f393-e0a9-e50e24dcca9e}";
    private static readonly string UARTWriteCharacteristic = "{6e400002-b5a3-f393-e0a9-e50e24dcca9e}";

    public string selectedCharacteristicId;
    Dictionary<string, Dictionary<string, string>> devices = new Dictionary<string, Dictionary<string, string>>();
    string lastError;

    private string[] sensorData = new string[SENSOR_AMOUNT];
    public Sensor[] sensors = new Sensor[SENSOR_AMOUNT];

    private List<string> csvData = new List<string>();

    private string[] baseReadings = new string[SENSOR_AMOUNT];

    public bool[] enabledSensors = new bool[SENSOR_AMOUNT];

    // Start is called before the first frame update
    void Start()
    {
        bluetoothPanel.gameObject.SetActive(true);
        finished.interactable = false;
        scanResultRoot = deviceScanResultProto.transform.parent;
        deviceScanResultProto.transform.SetParent(scanResultRoot.transform.parent);
        deviceScanResultProto.gameObject.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        BleApi.ScanStatus status;
        if (isScanningDevices)
        {
            BleApi.DeviceUpdate res = new BleApi.DeviceUpdate();
            do
            {
                status = BleApi.PollDevice(ref res, false);
                if (status == BleApi.ScanStatus.AVAILABLE)
                {
                    if (!devices.ContainsKey(res.id))
                        devices[res.id] = new Dictionary<string, string>() {
                            { "name", "" },
                            { "isConnectable", "False" }
                        };
                    if (res.nameUpdated)
                        devices[res.id]["name"] = res.name;
                    if (res.isConnectableUpdated)
                        devices[res.id]["isConnectable"] = res.isConnectable.ToString();
                    // consider only devices which have a name and which are connectable
                    if (devices[res.id]["name"] != "" && devices[res.id]["isConnectable"] == "True")
                    {
                        // add new device to list
                        GameObject g = Instantiate(deviceScanResultProto, scanResultRoot);
                        g.name = res.id;
                        g.transform.GetChild(0).GetComponent<Text>().text = devices[res.id]["name"];
                        g.transform.GetChild(1).GetComponent<Text>().text = res.id;
                        g.gameObject.SetActive(true);
                    }
                }
                else if (status == BleApi.ScanStatus.FINISHED)
                {
                    isScanningDevices = false;
                    deviceScanButtonText.text = "Scan devices";
                    statusText.text = "Finished scanning devices";
                    StartServiceScan();
                }
            } while (status == BleApi.ScanStatus.AVAILABLE);
        }
        if (isScanningServices)
        {
            BleApi.Service res = new BleApi.Service();
            do
            {
                status = BleApi.PollService(out res, false);
                if (status == BleApi.ScanStatus.FINISHED)
                {
                    isScanningServices = false;
                    statusText.text = "Finished scanning services";
                    StartCharacteristicScan();
                }
            } while (status == BleApi.ScanStatus.AVAILABLE);
        }
        if (isScanningCharacteristics)
        {
            BleApi.Characteristic res = new BleApi.Characteristic();
            do
            {
                status = BleApi.PollCharacteristic(out res, false);
                if (status == BleApi.ScanStatus.AVAILABLE)
                {
                    string name = res.userDescription != "no description available" ? res.userDescription : res.uuid;
                    selectedCharacteristicId = "{6e400003-b5a3-f393-e0a9-e50e24dcca9e}";
                }
                else if (status == BleApi.ScanStatus.FINISHED)
                {
                    isScanningCharacteristics = false;
                    statusText.text = "Finished scanning characteristics";
                    Subscribe();
                }
            } while (status == BleApi.ScanStatus.AVAILABLE);
        }
        if (isSubscribed)
        {
            BleApi.BLEData res = new BleApi.BLEData();
            while (BleApi.PollData(out res, false))
            {
                // Get data from all sensors
                for(int i = 0; i < SENSOR_AMOUNT; i++)
                {
                    int baseline = (res.buf[1 + i*5] << 8) + res.buf[2 + i * 5];
                    int filtered = (res.buf[3 + i * 5] << 8) + res.buf[4 + i * 5];
                    int change = Mathf.Abs(baseline - filtered);
                    int isTouching = res.buf[0 + i * 5];


                    sensors[i].touched = isTouching != 0;
                    sensors[i].change = change;

                    if (baseReadings[i] == null)
                        baseReadings[i] = "0";

                    // Bluetooth menu text
                    if (!isSetupFisnished){
                        sensorData[i] = "id:" + i + " Touched:" + isTouching + " Analog change:" + change.ToString();
                        subscribeText.text = string.Join("\n\n", sensorData);
                    }

                    // Add to list
                    else if(isRecording)
                    {
                        if (enabledSensors[i])
                            sensorData[i] = isTouching.ToString();
                        else sensorData[i] = "0";
                    }
                }
                if (isRecording)
                {
                    csvData.Add(string.Join(",", sensorData));
                }
            }
        }
        {
            // log potential errors
            BleApi.ErrorMessage res = new BleApi.ErrorMessage();
            BleApi.GetError(out res);
            if (lastError != res.msg)
            {
                errorText.text = res.msg;
                lastError = res.msg;
            }
        }
    }
    private void OnApplicationQuit()
    {
        BleApi.Quit();
    }

    public void StartStopDeviceScan()
    {
        if (!isScanningDevices)
        {
            // start new scan
            for (int i = scanResultRoot.childCount - 1; i >= 0; i--)
                Destroy(scanResultRoot.GetChild(i).gameObject);
            BleApi.StartDeviceScan();
            isScanningDevices = true;
            deviceScanButtonText.text = "Stop scan";
            statusText.text = "Scanning devices";
        }
        else
        {
            // stop scan
            isScanningDevices = false;
            BleApi.StopDeviceScan();
            deviceScanButtonText.text = "Start scan";
            statusText.text = "Stopped scanning devices";
        }
    }

    public void SelectDevice(GameObject data)
    {
        for (int i = 0; i < scanResultRoot.transform.childCount; i++)
        {
            var child = scanResultRoot.transform.GetChild(i).gameObject;
            child.transform.GetChild(0).GetComponent<Text>().color = child == data ? Color.red :
                deviceScanResultProto.transform.GetChild(0).GetComponent<Text>().color;
        }
        selectedDeviceId = data.name;

        StartServiceScan();
    }

    public void StartServiceScan()
    {
        if (!isScanningServices)
        {
            BleApi.ScanServices(selectedDeviceId);
            isScanningServices = true;
            statusText.text = "Scanning services";
        }
    }

    public void StartCharacteristicScan()
    {
        if (!isScanningCharacteristics)
        {
            BleApi.ScanCharacteristics(selectedDeviceId, UARTServiceId);
            isScanningCharacteristics = true;
            statusText.text = "Scanning characteristics";
        }
    }

    public void Subscribe()
    {
        BleApi.SubscribeCharacteristic(selectedDeviceId, UARTServiceId, selectedCharacteristicId, false);
        isSubscribed = true;
        statusText.text = "Subscribed to UART";
        resetButton.interactable = true;
        finished.interactable = true;
    }

    public void FinishedSetup()
    {
        bluetoothPanel.gameObject.SetActive(false);
        isSetupFisnished = true;
        ResetCube();
    }

    public struct Sensor
    {
        public bool touched;
        public int change;
    }

    public void StartSensorData() {
        Write(0x01);
    }

    public void StopSensorData() {
        Write(0x00);
    }

    public void ResetCube() {
        Write(0x03);
    }

    private void Write(byte msg)
    {
        selectedCharacteristicId = "{6e400002-b5a3-f393-e0a9-e50e24dcca9e}";
        Subscribe();
        byte[] payload = new byte[] { msg };
        BleApi.BLEData data = new BleApi.BLEData
        {
            buf = new byte[512],
            size = (short)payload.Length,
            deviceId = selectedDeviceId,
            serviceUuid = UARTServiceId,
            characteristicUuid = selectedCharacteristicId
        };
        for (int i = 0; i < payload.Length; i++)
            data.buf[i] = payload[i];

        BleApi.SendData(in data, false);
        selectedCharacteristicId = "{6e400003-b5a3-f393-e0a9-e50e24dcca9e}";
    }

    public void StartCSVData()
    {
        csvData.Clear();
        isRecording = true;
    }
    public void StopCSVData()
    {
        isRecording = false;
    }
    public List<string> GetCSVData()
    {
        return csvData;
    }

    
}
