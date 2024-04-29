using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;
using TMPro;
using Random = System.Random;

namespace C2M2.NeuronalDynamics.Interaction {
    using Utils.DebugUtils;
    using NeuronalDynamics.Visualization;
    public class CellPreviewer : MonoBehaviour
    {
        [Tooltip("Cell path relative to StreamingAssets")]
        /// <summary>
        /// Cell path relative to StreamingAssets
        /// </summary>
        public string cellsPath = "NeuronalDynamics" + Path.DirectorySeparatorChar + "Geometries";
        public GameObject previewWindowPrefab = null;
        public NDSimulationLoader loader = null;
        public bool renderWalls = true;
        public Color32 windowColor = Color.black;
        public GameObject ErrorWindow = null;

        /// <summary>
        /// Colors ot use for the 1D cell renderings. More than cellColors.Length cells will repeat these colors
        /// </summary>
        public Color32[] cellColors = new Color32[]
        {
            new Color32(255, 200, 0, 255),
            new Color32(0, 200, 0, 255),
            new Color32(0, 100, 255, 255),
            new Color32(200, 0, 0, 255)
        };

        /// <summary>
        /// Defines the normalized positions of the first x-y row of preview windows.
        /// </summary>
        /// <remarks>
        /// The default array represents normalized positions of placing up to four preview windows in a four-by-one alignment.
        /// if stackPos is true, this will automatically "stack" positionsNorm above and below this array in the y-axis
        /// </remarks>
        public Vector3[] positionsNorm = new Vector3[]
        {
            new Vector3(0, 0, 0),
            new Vector3(0, 0, 1),
            new Vector3(0, 0, -1),
            new Vector3(0, 0, -2),
        };
        [Tooltip("If true, positionsNorm will stack above and below on the y axis")]
        public bool stackPos = true;

        private void Awake()
        {
            // Make sure we have window preview prefab and a pointer to a simulation loader
            FindWindowPrefab();
            FindSimulationLoader();

            // Get possible geometries from given direcrory
            if(Application.platform==RuntimePlatform.OSXPlayer || Application.platform==RuntimePlatform.OSXEditor)
            {
                cellsPath = "NeuronalDynamics" + Path.AltDirectorySeparatorChar + "Geometries";
            }
            string fullPath = Application.dataPath + "/Resources" + "/Geometries";
            string[] geoms = GetGeometryNames(fullPath);

            if (geoms.Length > 0)
            {
                if(ErrorWindow != null) ErrorWindow.SetActive(false);

                // Make a preview window for each found geometry
                Vector3[] windowPositions = GetWindowPositions(geoms.Length);
                Color32[] previewColors = GetWindowColors(geoms.Length);
                for (int i = 0; i < windowPositions.Length; i++)
                {
                    InstantiatePreviewWindow(geoms[i], windowPositions[i], previewColors[i]);
                }
            }
            else
            {
                if (ErrorWindow != null)
                {
                    ErrorWindow.SetActive(true);
                    var go = ErrorWindow.transform.FindChildRecursive("FileName");
                    if (go != null)
                    {
                        TMPro.TextMeshProUGUI errorMsg = go.GetComponent<TMPro.TextMeshProUGUI>();
                        if (errorMsg != null)
                        {
                            errorMsg.text = "No cells found in " + fullPath;
                        }
                    }
                }
                Debug.LogWarning("No cells found in " + fullPath);
            }

            void FindWindowPrefab()
            {
                if (previewWindowPrefab == null)
                {
                    Object prefab = Resources.Load("Prefabs/CellPreviewWindow");
                    if (prefab == null)
                    {
                        Debug.LogError("No cell preview window prefab found!");
                        Destroy(this);
                    }
                    previewWindowPrefab = (GameObject)prefab;
                }
            }
            void FindSimulationLoader()
            {
                if (loader == null)
                {
                    loader = GetComponent<NDSimulationLoader>();
                    if (loader == null)
                    {
                        Debug.LogError("No simulation loader given to CellPreviewer!");
                        Destroy(this);
                    }
                }
            }
            string[] GetGeometryNames(string targetDir)
            {
                DirectoryInfo d = new DirectoryInfo(targetDir);

                FileInfo[] files = d.GetFiles("*.vrn");
                if (files.Length == 0) return new string[] { };

                string[] fileNames = new string[files.Length];
                for (int i = 0; i < files.Length; i++)
                {
                    fileNames[i] = files[i].Name;
                }
                return fileNames;
            }
            Vector3[] GetWindowPositions(int numWindows)
            {
                // Default Example: 0 <= numWindows <= 18
                numWindows = Utils.Math.Clamp(numWindows, 0, positionsNorm.Length * 3);
                if (numWindows == 0) return null;

                // We'll scale the default box positions by the preview window's edge length
                Vector3 windowLength = previewWindowPrefab.transform.localScale;

                Vector3[] positions = new Vector3[numWindows];

                for (int i = 0; i < numWindows; i++)
                {
                    // 0 <= i < 6 is the original row, 6 <= i < 12 is stacked ontop, 12 <= i < 18 is stacked below
                    int stackAmount = 0;
                    if (stackPos)
                    {
                        if (i < positionsNorm.Length) stackAmount = 0;
                        else if (i < positionsNorm.Length * 2) stackAmount = 1;
                        else if (i < positionsNorm.Length * 3) stackAmount = -1;
                    }

                    // possiblePositions only contains indices 0-5
                    
                    int ind = (i % positionsNorm.Length);


                    // Copies positions from possiblePositions, stacks if necessary, and scales positions using window length
                    positions[i] = new Vector3(windowLength.x * positionsNorm[ind].x, 
                        windowLength.y * (positionsNorm[ind].y + stackAmount), 
                        windowLength.z * positionsNorm[ind].z);
                        
                }

                return positions;
            }
            Color32[] GetWindowColors(int numColors)
            {
                numColors = Utils.Math.Clamp(numColors, 0, positionsNorm.Length * 3);
                if (numColors == 0) return null;

                Color32[] colors = new Color32[numColors];
                for (int i = 0; i < colors.Length; i++)
                {
                    colors[i] = cellColors[i % cellColors.Length];
                }
                Random rnd = new Random();

                // Randomize the order of the colors and return them
                return colors.OrderBy(x => rnd.Next()).ToArray();
            }
            void InstantiatePreviewWindow(string fileName, Vector3 position, Color color)
            {
                // Instantiate window
                GameObject go = Instantiate(previewWindowPrefab);
                go.transform.parent = transform;
                go.transform.localPosition = position;
                go.name = fileName + "Preview";

                // Find each wall in window, color accoring to input
                MeshRenderer[] prefabWalls = go.GetComponentsInChildren<MeshRenderer>();
                if (prefabWalls.Length > 0)
                {
                    foreach (MeshRenderer r in prefabWalls)
                    {
                        r.enabled = renderWalls;
                        r.material.color = windowColor;
                    }
                }

                // Start neuron cell previewer.
                NeuronCellPreview preview = go.GetComponentInChildren<NeuronCellPreview>();
                preview.vrnFileName = fileName;
                preview.windowColor = color;
                preview.loader = loader;
                preview.PreviewCell(fileName, color);

            }
        }
    }
}