using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System;
using System.Linq;
using TensorFlow;
using System.Runtime.InteropServices;
using System.Threading;
using System.Diagnostics;

public class GestureControl : MonoBehaviour
{
    public GameObject gesturesProto;
    Transform gesturesRoot;

    public GameObject gesturesPanel;
    public Button recordGestureButton;
    public Text recordGestureButtonText;
    public Text recordGestureStatusText;
    public Text recordingStatusText;
    public Text detectedGestureText;
    public Text detectGesturesButtonText;

    public GameObject bluetooth;
    private Bluetooth bluetoothScript;

    private bool recordingGestures = false;
    private bool recordingGesture = false;
    private bool detecting = false;

    private int recordingCount = 1;

    public static int DATA_PER_RECORDING = 200;
    public static int sensorCount = 12;
    private static string CSV_HEADER = "";
    private readonly static string GESTURES_FOLDER = "gestures/";


    private string selectedGesture;

    public InputField GestureNameInputField;

    private byte[] graphModel;
    private static readonly string graphModelPath = "frozen_model/frozen_graph.bytes";
    private TFGraph graph;
    private TFSession session;

    private float[,] inputArray_0;
    private float[,] inputArray_1 = null;
    private bool secondWindow = true;
    private int previousDetection = -1;


    private List<GameObject> gestures = new List<GameObject>();

    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern IntPtr PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);



    void Start()
    {
        // Create gestures dictionary if does not exist
        if (!Directory.Exists(GESTURES_FOLDER.Substring(0, GESTURES_FOLDER.Length - 1)))
            Directory.CreateDirectory(GESTURES_FOLDER.Substring(0, GESTURES_FOLDER.Length - 1));

        // Open TensorFlow graph
        graphModel = File.ReadAllBytes(graphModelPath);
        graph = new TFGraph();
        graph.Import(graphModel);
        session = new TFSession(graph);

        gesturesRoot = gesturesProto.transform.parent;
        gesturesProto.gameObject.SetActive(false);
        bluetoothScript = bluetooth.GetComponent<Bluetooth>();

        // Set csv header
        for (int i = 1; i <= sensorCount; i++)
            CSV_HEADER += "t" + i + ",";
        CSV_HEADER = CSV_HEADER.Substring(0, CSV_HEADER.Length - 1);

        // Add gestures
        DirectoryInfo dir = new DirectoryInfo(GESTURES_FOLDER.Substring(0, GESTURES_FOLDER.Length - 1));
        FileInfo[] info = dir.GetFiles();
        foreach (FileInfo f in info)
        {
            string name = f.Name.Substring(0, f.Name.Length - f.Extension.Length);
            GameObject g = Instantiate(gesturesProto, gesturesRoot);
            gestures.Add(g);
            g.name = name;
            g.transform.GetChild(0).GetComponent<Text>().text = name;

            if(name != "0Noise")
            g.gameObject.SetActive(true);
        }
        gestures = gestures.OrderBy(go => go.name).ToList();

    }

    void Update()
    {
        // Record gestures to .csv
        if (recordingGestures)
        {
            if (Input.GetKeyDown("space"))
            {
                bluetoothScript.StartCSVData();
                recordingGesture = true;
            }
            if (recordingGesture)
            {
                int count = bluetoothScript.GetCSVData().Count;
                recordingStatusText.text = "Recording " + recordingCount + " progress " + count + "/" + DATA_PER_RECORDING;
                if (count >= DATA_PER_RECORDING)
                {
                    bluetoothScript.StopCSVData();
                    recordingCount++;
                    recordingGesture = false;
                    using (StreamWriter sw = File.AppendText(GESTURES_FOLDER + selectedGesture + ".csv"))
                    {
                        foreach (string s in bluetoothScript.GetCSVData().GetRange(0, DATA_PER_RECORDING))
                            sw.WriteLine(s);
                    }
                }
            }
        }
        // Gesture detection
        else if (detecting)
        {
            int count = bluetoothScript.GetCSVData().Count;
            if (count >= DATA_PER_RECORDING / 2 && inputArray_1 != null)
            {
                List<string> data = bluetoothScript.GetCSVData().GetRange(0, DATA_PER_RECORDING / 2);
                for (int i = 0; i < DATA_PER_RECORDING / 2; i++)
                {
                    float[] linearr = Array.ConvertAll(data[i].Split(','), float.Parse);
                    for (int a = 0; a < 12; a++)
                    {
                        inputArray_1[DATA_PER_RECORDING / 2 + i, a] = linearr[a];
                    }
                }
                ResetDetection();
                Detect(inputArray_1);
                inputArray_1 = null;
            }
            else if (count >= DATA_PER_RECORDING)
            {
                bluetoothScript.StopCSVData();
                recordingCount++;
                List<string> data = bluetoothScript.GetCSVData().GetRange(0, DATA_PER_RECORDING);
                inputArray_0 = new float[200, 12];
                for (int i = 0; i < DATA_PER_RECORDING; i++)
                {
                    float[] linearr = Array.ConvertAll(data[i].Split(','), float.Parse);
                    for (int a = 0; a < 12; a++)
                    {
                        inputArray_0[i, a] = linearr[a];
                    }
                }
                ResetDetection();
                Detect(inputArray_0);
                inputArray_1 = inputArray_0;
                bluetoothScript.StartCSVData();
            }
        }
    }

    public void OpenGesturesMenu()
    {
        gesturesPanel.gameObject.SetActive(true);
    }

    public void AddGesture()
    {
        GameObject g = Instantiate(gesturesProto, gesturesRoot);
        gestures.Add(g);
        gestures = gestures.OrderBy(go => go.name).ToList();

        g.name = GestureNameInputField.text;
        g.transform.GetChild(0).GetComponent<Text>().text = GestureNameInputField.text;
        g.gameObject.SetActive(true);
    }

    public void DeleteGesture(GameObject gesture)
    {
        gestures.Remove(gesture);
        gestures = gestures.OrderBy(go => go.name).ToList();
        Destroy(gesture);
        if (File.Exists(GESTURES_FOLDER + gesture.name + ".csv"))
            File.Delete(GESTURES_FOLDER + gesture.name + ".csv");
    }

    public void SelectGesture(GameObject data)
    {
        for (int i = 1; i < gesturesRoot.transform.childCount; i++) {
            GameObject child = gesturesRoot.transform.GetChild(i).gameObject;
            child.transform.GetChild(0).GetComponent<Text>().color = child == data ? Color.red :
                Color.black;
        }
        selectedGesture = data.name;
        recordGestureButton.interactable = true;
    }

    public void RecordGesture()
    {
        if (recordingGestures)
        {
            recordGestureButtonText.text = "Record gesture";
            recordGestureStatusText.text = "The gesture has already been recorded.";
            recordingGestures = false;
            recordingCount = 1;
        }
        else
        {
            recordGestureStatusText.text = "The gesture is currently being recorded.";
            recordGestureButtonText.text = "Stop recording";
            recordingGestures = true;

            if (!File.Exists(GESTURES_FOLDER + selectedGesture + ".csv"))
                using (StreamWriter sw = File.CreateText(GESTURES_FOLDER + selectedGesture + ".csv"))
                    sw.WriteLine(CSV_HEADER);
        }
    }
    public void ToggleDetection()
    {
        if (detecting)
        {
            detecting = false;
            detectGesturesButtonText.text = "Start Detection";
            recordingStatusText.text = "";
        }
        else
        {
            graphModel = File.ReadAllBytes(graphModelPath);
            graph = new TFGraph();
            graph.Import(graphModel);
            session = new TFSession(graph);

            detecting = true;
            bluetoothScript.StartCSVData();
            detectGesturesButtonText.text = "Stop detection";
            recordingStatusText.text = "Detecting";
        }
    }

    private void ResetDetection()
    {
        for (int i = 1; i < gesturesRoot.transform.childCount; i++)
        {
            GameObject child = gesturesRoot.transform.GetChild(i).gameObject;
            if (child.name != selectedGesture)
                child.transform.GetChild(0).GetComponent<Text>().color = Color.black;
        }
        detectedGestureText.text = "";
    }


    // Starts the training application
    public void TrainModel()
    {
        Process p = new Process();
        p.StartInfo.UseShellExecute = true;
        p.StartInfo.WorkingDirectory = Directory.GetCurrentDirectory() + "/train";
        p.StartInfo.FileName = Directory.GetCurrentDirectory() + "/train/train.exe";
        p.Start();
    }
    
    // Data preprocessing and gesture detection
    private void Detect(float[,] inputTensor)
    {

        int currentDetection = 0;
        int firstHalfDetection = 0;

        inputTensor = Transpose(inputTensor);
        float[,,] inputArray = new float[1, 200, 12];
        for (int i = 0; i < sensorCount; i++)
        {
            int count = 0;
            for (int a = 0; a < DATA_PER_RECORDING; a++)
            {
                if (inputTensor[i, a] == 1f)
                    count++;
            }
            if (count > DATA_PER_RECORDING / 3)
                for (int a = 0; a < DATA_PER_RECORDING; a++)
                    inputTensor[i, a] = 0;
        }

        for (int i = 0; i < DATA_PER_RECORDING; i++)
            for (int a = 0; a < sensorCount; a++)
                inputArray[0, i, a] = inputTensor[a, i];


        float[] values = GetDetectionOutput(inputArray);
        values[0] = 0;
        float maxVal = values.Max();

        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] == maxVal && maxVal > 0.4)
            {
                currentDetection = i;
                break;
            }
        }

        for (int i = DATA_PER_RECORDING / 2; i < DATA_PER_RECORDING; i++)
            for (int a = 0; a < sensorCount; a++)
                inputArray[0, i, a] = 0;

        values = GetDetectionOutput(inputArray);
        values[0] = 0;

        maxVal = values.Max();

        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] == maxVal && maxVal > 0.4)
            {
                firstHalfDetection = i;
            }
        }

        if (currentDetection != 0 && (firstHalfDetection != previousDetection || previousDetection == 0 || firstHalfDetection != currentDetection || firstHalfDetection == 0))
        {
            const uint WM_KEYDOWN = 0x100;
            gestures[currentDetection].transform.GetChild(0).GetComponent<Text>().color = Color.red;

            detectedGestureText.text = "\nDetected: " + gestures[currentDetection].name;
            // Send key
            string key = gestures[currentDetection].transform.GetChild(2).GetComponent<InputField>().text;
            IntPtr hwnd = IntPtr.Zero;
            hwnd = GetForegroundWindow();
            PostMessage(hwnd, WM_KEYDOWN, (IntPtr)StringToKeycode(key), IntPtr.Zero);
        }
        previousDetection = currentDetection;


        ToggleSecondWindow();

    }

    // Get output from TensorFlow model
    private float[] GetDetectionOutput(float[,,] input)
    {
        if (session == null)
            session = new TFSession(graph);

        TFSession.Runner runner = session.GetRunner();
        runner.AddInput(graph["input_1"][0], input);
        runner.Fetch(graph["sequential/dense/Softmax"][0]);
        TFTensor output = runner.Run()[0];
        float[,] val = output.GetValue(false) as float[,];

        return val.Cast<float>().ToArray();
    }

    private void ToggleSecondWindow()
    {
        if (secondWindow == true)
            secondWindow = false;
        else
            secondWindow = true;
    }


    public float[,] Transpose(float[,] matrix)
    {
        int w = matrix.GetLength(0);
        int h = matrix.GetLength(1);

        float[,] result = new float[h, w];

        for (int i = 0; i < w; i++)
        {
            for (int j = 0; j < h; j++)
            {
                result[j, i] = matrix[i, j];
            }
        }

        return result;
    }

    // Maps some of the popular keys to windows form keycodes
    private int StringToKeycode(string keyString)
    {
        if (keyString == "" || keyString == null)
            return 0;
        keyString = keyString.ToUpper();
        switch(keyString)
        {
            case "SPACE":
                return 32;
            case "UP":
                return 38;
            case "DOWN":
                return 40;
            case "LEFT":
                return 37;
            case "RIGHT":
                return 39;
            case "CTRL":
                return 17;
            case "ALT":
                return 18;
            case "BACK":
                return 8;
            default:
                return keyString.ToCharArray()[0];
        }
    }

}
