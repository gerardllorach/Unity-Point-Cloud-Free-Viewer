// Original project by Gerard Llorach (2014)
// Updated by Oliver Dawkins and Dominic Zisch (2017) to visualise points using height and intensity gradients

using UnityEngine;
using System.Collections;
using System.IO;

public class PointCloudManager : MonoBehaviour {

	// File location
	public string dataPath;
	private string filename;
	public Material matVertex;

    // Methods to colour points
    public enum cpb {Default, RGB, Height, Intensity}; 
    public cpb colourPointsBy = cpb.RGB;
    public Color defaultPointColour;
    public Gradient colourGradient;

    // Processing GUI
    private float progress = 0;
	private new string guiText;
	private bool loaded = false;

	// Point cloud properties
	private GameObject pointCloud;

	public float scale = 1;
    public bool relocateToOrigin = false;
    public bool invertYZ = false;
	public bool forceReload = false;

	public int numPoints;
	public int numPointGroups;
	private int limitPoints = 65000;

	private Vector3[] points;
	private Color[] colors;
	private Vector3 minValue;

    // Point height properties
    public float minHeight;
    public float maxHeight;
    private float heightDiff;
    private float localDiff;

    // Point intensity properties
    public float minIntensity;
    public float maxIntensity;
    private float intensityDiff;
    private float relativeDiff;

    void Start () {

        //Calculate height difference for the visualising height gradient
        heightDiff = maxHeight - minHeight;

        //Calculate intensity difference for visualising intensity gradient
        intensityDiff = maxIntensity - minIntensity;

        // Create Resources folder
        createFolders();

		// Get Filename
		filename = Path.GetFileName(dataPath);

		loadScene();
	}

	void loadScene(){
		// Check if the PointCloud was loaded previously
		if(!Directory.Exists (Application.dataPath + "/Resources/PointCloudMeshes/" + filename)){
			UnityEditor.AssetDatabase.CreateFolder ("Assets/Resources/PointCloudMeshes", filename);
			loadPointCloud ();
		} else if (forceReload){
			UnityEditor.FileUtil.DeleteFileOrDirectory(Application.dataPath + "/Resources/PointCloudMeshes/" + filename);
			UnityEditor.AssetDatabase.Refresh();
			UnityEditor.AssetDatabase.CreateFolder ("Assets/Resources/PointCloudMeshes", filename);
			loadPointCloud ();
		} else
			// Load stored PointCloud
			loadStoredMeshes();
	}	
    	
	void loadPointCloud(){
		// Check what file exists
		if (File.Exists (Application.dataPath + dataPath + ".xyz")) 
			// Load XYZ
			StartCoroutine ("loadXYZ", dataPath + ".xyz");
		else 
			Debug.Log ("File '" + dataPath + "' could not be found"); 
		
	}
	
	// Load stored PointCloud
	void loadStoredMeshes(){

		Debug.Log ("Using previously loaded PointCloud: " + filename);

		GameObject pointGroup = Instantiate(Resources.Load ("PointCloudMeshes/" + filename)) as GameObject;

		loaded = true;
	}
	
	// Start Coroutine of reading the points from the XYZ file and creating the meshes
	IEnumerator loadXYZ(string dPath){

		// Read file
		numPoints = File.ReadAllLines (Application.dataPath + dPath).Length;

		StreamReader sr = new StreamReader (Application.dataPath + dPath);

		points = new Vector3[numPoints];
		colors = new Color[numPoints];
		minValue = new Vector3();
		
		for (int i = 0; i< numPoints; i++){
			string[] buffer = sr.ReadLine ().Split(',');

			if (!invertYZ)
				points[i] = new Vector3 (float.Parse (buffer[0])*scale, float.Parse (buffer[1])*scale,float.Parse (buffer[2])*scale);
			else
				points[i] = new Vector3 (float.Parse (buffer[0])*scale, float.Parse (buffer[2])*scale,float.Parse (buffer[1])*scale);

            // Test enum for technique to colour points
            // Apply default point colour
            if (colourPointsBy == cpb.Default)
            {
                colors[i] = defaultPointColour;
            }

                // Colour points by RGB values
                if (colourPointsBy == cpb.RGB)
            {
                if (buffer.Length >= 5)
                    colors[i] = new Color(int.Parse(buffer[3]) / 255.0f, int.Parse(buffer[4]) / 255.0f, int.Parse(buffer[5]) / 255.0f);
                else
                    colors[i] = defaultPointColour;
            }

            // TO DO - Automate calculation of minHeight and maxHeight
            // Colour points by Height
            else if (colourPointsBy == cpb.Height)
            {
                if (!invertYZ)
                    localDiff = float.Parse(buffer[1]) - minHeight;
                else
                    localDiff = float.Parse(buffer[2]) - minHeight;
                colors[i] = colourGradient.Evaluate(localDiff / heightDiff);
            }

            //TO DO - Automate calculation of minIntensity and maxIntensity
            // Colour points by intensity 
            else if (colourPointsBy == cpb.Intensity)
            {
                relativeDiff = float.Parse(buffer[6]) - minIntensity;
                colors[i] = colourGradient.Evaluate(relativeDiff / intensityDiff);
            }

            // Relocate points near the origin
            if (relocateToOrigin == true)
            {
                calculateMin(points[i]);
            }

            // Processing GUI
            progress = i *1.0f/(numPoints-1)*1.0f;
			if (i%Mathf.FloorToInt(numPoints/20) == 0)
            {
				guiText=i.ToString() + " out of " + numPoints.ToString() + " loaded";
				yield return null;
			}
		}

		// Instantiate Point Groups
		numPointGroups = Mathf.CeilToInt (numPoints*1.0f / limitPoints*1.0f);

		pointCloud = new GameObject (filename);

		for (int i = 0; i < numPointGroups-1; i ++) {
			InstantiateMesh (i, limitPoints);
			if (i%10==0){
				guiText = i.ToString() + " out of " + numPointGroups.ToString() + " PointGroups loaded";
				yield return null;
			}
		}
		InstantiateMesh (numPointGroups-1, numPoints- (numPointGroups-1) * limitPoints);

		//Store PointCloud
		UnityEditor.PrefabUtility.SaveAsPrefabAsset(pointCloud, "Assets/Resources/PointCloudMeshes/" + filename + ".prefab");

		loaded = true;
	}
	
	void InstantiateMesh(int meshInd, int nPoints){
		// Create Mesh
		GameObject pointGroup = new GameObject (filename + meshInd);
		pointGroup.AddComponent<MeshFilter> ();
		pointGroup.AddComponent<MeshRenderer> ();
		pointGroup.GetComponent<Renderer>().material = matVertex;

		pointGroup.GetComponent<MeshFilter> ().mesh = CreateMesh (meshInd, nPoints, limitPoints);
		pointGroup.transform.parent = pointCloud.transform;

		// Store Mesh
		UnityEditor.AssetDatabase.CreateAsset(pointGroup.GetComponent<MeshFilter> ().mesh, "Assets/Resources/PointCloudMeshes/" + filename + @"/" + filename + meshInd + ".asset");
		UnityEditor.AssetDatabase.SaveAssets ();
		UnityEditor.AssetDatabase.Refresh();
	}

	Mesh CreateMesh(int id, int nPoints, int limitPoints){
		
		Mesh mesh = new Mesh ();
		
		Vector3[] myPoints = new Vector3[nPoints]; 
		int[] indecies = new int[nPoints];
		Color[] myColors = new Color[nPoints];

		for(int i=0;i<nPoints;++i) {
			myPoints[i] = points[id*limitPoints + i] - minValue;
			indecies[i] = i;
			myColors[i] = colors[id*limitPoints + i];
		}


		mesh.vertices = myPoints;
		mesh.colors = myColors;
		mesh.SetIndices(indecies, MeshTopology.Points,0);
		mesh.uv = new Vector2[nPoints];
		mesh.normals = new Vector3[nPoints];


		return mesh;
	}

	void calculateMin(Vector3 point){
		if (minValue.magnitude == 0)
			minValue = point;


		if (point.x < minValue.x)
			minValue.x = point.x;
		if (point.y < minValue.y)
			minValue.y = point.y;
		if (point.z < minValue.z)
			minValue.z = point.z;
	}

	void createFolders(){
		if(!Directory.Exists (Application.dataPath + "/Resources/"))
			UnityEditor.AssetDatabase.CreateFolder ("Assets", "Resources");

		if (!Directory.Exists (Application.dataPath + "/Resources/PointCloudMeshes/"))
			UnityEditor.AssetDatabase.CreateFolder ("Assets/Resources", "PointCloudMeshes");
	}

	void OnGUI(){


		if (!loaded){
			GUI.BeginGroup (new Rect(Screen.width/2-100, Screen.height/2, 400.0f, 20));
			GUI.Box (new Rect (0, 0, 200.0f, 20.0f), guiText);
			GUI.Box (new Rect (0, 0, progress*200.0f, 20), "");
			GUI.EndGroup ();
		}
	}

}
