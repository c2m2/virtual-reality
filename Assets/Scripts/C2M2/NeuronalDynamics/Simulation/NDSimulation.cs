using System;
using System.Collections.Generic;
using System.IO;
using C2M2.NeuronalDynamics.UGX;
using UnityEngine;
using DiameterAttachment = C2M2.NeuronalDynamics.UGX.IAttachment<C2M2.NeuronalDynamics.UGX.DiameterData>;
using MappingAttachment = C2M2.NeuronalDynamics.UGX.IAttachment<C2M2.NeuronalDynamics.UGX.MappingData>;
using Math = C2M2.Utils.Math;
using C2M2.Simulation;
using C2M2.Utils.DebugUtils;
using C2M2.Utils.MeshUtils;
using Grid = C2M2.NeuronalDynamics.UGX.Grid;
using C2M2.NeuronalDynamics.Visualization.VRN;
using C2M2.NeuronalDynamics.Interaction;
using C2M2.NeuronalDynamics.Interaction.UI;
using C2M2.Interaction.UI;
using System.Linq;
using C2M2.Utils;

namespace C2M2.NeuronalDynamics.Simulation {

    /// <summary>
    /// Provide an interface for 1D neuron-surface simulations to be visualized and interacted with
    /// </summary>
    /// <remarks>
    /// 1D Neuron surface simulations should derive from this class.
    /// </remarks>
    public abstract class NDSimulation : MeshSimulation {
        public int simID = -1; // simulation ID

        public new NDSimulationManager Manager { get { return GameManager.instance.simulationManager; } }
        private double visualInflation = 1;
        public double VisualInflation
        {
            get { return visualInflation; }
            set
            {
                if (visualInflation != value)
                {
                    visualInflation = value;

                    Update2DGrid();

                    VisualMesh = Grid2D.Mesh;
                    OnVisualInflationChange?.Invoke(visualInflation);
                }
            }
        }

        public delegate void OnVisualInflationChangeDelegate(double newInflation);
        public event OnVisualInflationChangeDelegate OnVisualInflationChange;

        private int refinementLevel = 0;
        public int RefinementLevel
        {
            get { return refinementLevel; }
            set
            {
                if (refinementLevel != value && value >= 0)
                {
                    refinementLevel = value;
                    UpdateGrid1D();
                }
            }
        }

        private Dictionary<double, Mesh> meshCache = new Dictionary<double, Mesh>();

        public NeuronClampManager clampManager = null;
        internal readonly object clampLock = new object();

        public NDGraphManager graphManager = null;

        [Header ("1D Visualization")]
        public bool visualize1D = false;
        public Color32 color1D = Color.yellow;
        public float lineWidth1D = 0.005f;

        private InfoPanel infoPanel = null;

        public GameObject infoPanelPrefab = null;

        public GameObject controlPanel = null;

        // Need mesh options for each refinement, diameter level
        [Tooltip("Name of the vrn file within Assets/StreamingAssets/NeuronalDynamics/Geometries")]
        public string vrnFileName = "test.vrn";
        private VrnReader vrnReader = null;
        /// <summary>
        /// Used to read cell data from .vrn archives
        /// </summary>
        public VrnReader VrnReader
        {
            get
            {
                if (vrnReader == null)
                {
                    char sl = Path.DirectorySeparatorChar;
                    if (!vrnFileName.EndsWith(".vrn")) vrnFileName = vrnFileName + ".vrn";
                    vrnReader = new VrnReader(Application.streamingAssetsPath + sl + "NeuronalDynamics" + sl + "Geometries" + sl + vrnFileName);
                }
                return vrnReader;
            }
            private set { vrnReader = value; }
        }
        /// <summary>
        /// Includes info like cell species, strain, and archive
        /// </summary>
        public VrnReader.MetaInfo MetaInfo { get { return (VrnReader.MetaInfo)vrnReader.GetMetaInfo(); } }

        private Grid grid1D = null;
        public Grid Grid1D
        {
            get {
                return grid1D;
            }
            set
            {
                grid1D = value;
            }
        }

        public Vector3[] Verts1D { get { return grid1D.Mesh.vertices; } }

        private Grid grid2D = null;
        public Grid Grid2D
        {
            get
            {
                return grid2D;
            }
            set
            {
                grid2D = value;
            }
        }
        public Neuron Neuron { get; set; } = null;

        private float averageDendriteRadius = 0;
        public float AverageDendriteRadius
        {
            get
            {
                if (averageDendriteRadius == 0)
                {
                    averageDendriteRadius = (float)Neuron.nodes.Select(node => node.NodeRadius).Average();
                }
                return averageDendriteRadius;
            }
        }

        // Stores the information from mapping in an array of structs.
        // Performs much better than using mapping directly.
        private Vert3D1DPair[] map = null;
        public Vert3D1DPair[] Map
        {
            get
            {
                if(map == null)
                {
                    map = new Vert3D1DPair[Mapping.Data.Count];
                    for(int i = 0; i < Mapping.Data.Count; i++)
                    {
                        map[i] = new Vert3D1DPair(Mapping.Data[i].Item1, Mapping.Data[i].Item2, Mapping.Data[i].Item3);
                    }
                }
                return map;
            }
        }
        private MappingInfo mapping = default;
        private MappingInfo Mapping
        {
            get
            {
                if(mapping.Equals(default(MappingInfo)))
                {
                    mapping = (MappingInfo)MapUtils.BuildMap(Grid1D, Grid2D);
                }
                return mapping;
            }
            set
            {
                mapping = (MappingInfo)MapUtils.BuildMap(Grid1D, Grid2D);
            }
        }

        void ShowInfoPanel(bool show, RaycastHit hit)
        {
            if (infoPanel == null)
            {
                infoPanel = Instantiate(infoPanelPrefab, transform).GetComponent<InfoPanel>();
            }
            infoPanel.gameObject.SetActive(show);
            if(show)
            {
                int id = GetNearestPoint(hit);
                infoPanel.unit = unit;
                infoPanel.Vertex = id;
                infoPanel.Power = Get1DValues()[id] * unitScaler;
                Vector3 pos = new Vector3(Verts1D[id].x, Verts1D[id].y+2, Verts1D[id].z); //offset so the popup is not in the middle of the dendrite
                infoPanel.transform.localPosition = pos;
            }
        }

        protected override void PostSolveStep(int t)
        {
            ApplyInteractionVals();
            SetOutputValues();
            void ApplyInteractionVals()
            {
                /// Apply clamp values, if there are any clamps
                lock(clampLock)
                {
                    if(clampManager.clamps.Count > 0)
                    {
                        List<(int, double)> clampValues = new List<(int, double)>();
                        for(int i = 0; i < clampManager.clamps.Count; i++)
                        {
                            if(clampManager.clamps[i] != null && clampManager.clamps[i].FocusVert != -1 && clampManager.clamps[i].ClampLive)
                            {
                                clampValues.Add((clampManager.clamps[i].FocusVert, clampManager.clamps[i].ClampPower));
                            }
                        }
                        Set1DValues(clampValues.ToArray<(int, double)>());
                    }
                }

                /// Apply synapse values, if there are any synapses
                if (Manager.synapseManager.synapses.Count > 0)
                {
                    List<(Synapse, Synapse)> synapses = new List<(Synapse, Synapse)>(); //pre (Item1) and post (Item2) synapses
                    
                    // Gather a list of each synapse with their post on the current sim
                    foreach ((Synapse, Synapse) syn in Manager.synapseManager.synapses)
                    {
                        Synapse preSynapse = syn.Item1;
                        Synapse postSynapse = syn.Item2;
                        if (this == postSynapse.simulation)
                        {
                            // Set the synapse voltage to what the voltage is at the 1D vertex
                            preSynapse.ActivationTime = 0.0;
                            postSynapse.ActivationTime = 0.0;

                            synapses.Add((preSynapse, postSynapse));
                        }
                    }

                    // Set the post synaptic current for each pair of pre/post synapses i.e. each tuple (Synapse,Synapse)
                    SetSynapseCurrent(synapses);
                }
                
                // Apply raycast values
                if (raycastHits.Length > 0)
                {
                    Set1DValues(raycastHits);
                }
            }
        }

        internal abstract void SetOutputValues();

        /// <summary>
        /// Controls Synapses Behavior
        /// </summary>
        /// <param name="synapses">pre (Item1) and post (Item2) synapses</param>
        internal abstract void SetSynapseCurrent(List<(Synapse, Synapse)> synapses);

        protected override void OnAwakePost(Mesh viz)
        {
            base.OnAwakePost(viz);
            infoPanelPrefab = (GameObject)Resources.Load("Prefabs" + Path.DirectorySeparatorChar + "NeuronalDynamics" + Path.DirectorySeparatorChar + "PointInfo");

            defaultRaycastEvent.OnHover.AddListener((hit) =>
            {
                ShowInfoPanel(true, hit);
            });
            defaultRaycastEvent.OnHoverEnd.AddListener((hit) =>
            {
                ShowInfoPanel(false, hit);
            });
            defaultRaycastEvent.OnHoldPress.AddListener((hit) =>
            {
                ShowInfoPanel(true, hit);
            });
            defaultRaycastEvent.OnEndPress.AddListener((hit) =>
            {
                ShowInfoPanel(false, hit);
            });

        }
        /// <summary>
        /// Translate 1D vertex values to 3D values and pass them upwards for visualization
        /// </summary>
        /// <returns> One scalar value for each 3D vertex based on its 1D vert's scalar value </returns>
        public sealed override float[] GetValues () {
            /* double[] valsPrev1D = GetPrev1DValues(); */
            double[] vals1D = Get1DValues();
            double[] scalars3D = new double[Mapping.Data.Count];
            
            /* Debug.Log("Prev: "+valsPrev1D.ToString()); */
            /* Debug.Log("Curr: "+vals1D.ToString()); */

            if (vals1D == null) { return null; }

            for (int i = 0; i < Map.Length; i++) { // for each 3D point,

                // Take an weighted average using lambda
                // Equivalent to [lambda * v2 + (1 - lambda) * v1]
                scalars3D[i] = map[i].lambda * (vals1D[map[i].v2] - vals1D[map[i].v1]) + vals1D[map[i].v1];
            }

            // Update graphs
            foreach(NDGraph graph in graphManager.graphs)
            {
                graph.ndlinegraph.AddValue(1000*GetSimulationTime(), (float)vals1D[graph.FocusVert] * unitScaler);
            }

            return scalars3D.ToFloat();
        }

        /// <summary>
        /// Translate 3D vertex values to 1D values, and pass them downwards for interaction
        /// </summary>
        public sealed override void SetValues (RaycastHit hit) {
            int[] verts = HitToVertices(hit);
            (int, double)[] newValues = new (int, double)[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                newValues[i] = (verts[i], raycastHitValue);
            }
            SetValues(newValues);
        }
        /// <summary>
        /// Translate 3D vertex values to 1D values, and store the values to be applied to simulation values
        /// </summary>
        public void SetValues ((int, double)[] newValues) {
            // Reserve space for new1DValuess
            (int, double)[] new1Dvalues = new (int, double)[newValues.Length];
            // Receive values given to 3D vertices, translate them onto 1D vertices and apply values there
            for (int i = 0; i < newValues.Length; i++)
            {
                int vert3D = newValues[i].Item1;
                double val3D = newValues[i].Item2;

                // If lambda > 0.5, the vert is closer to v2 so apply val3D there
                int vert1D = (map[vert3D].lambda > 0.5) ? map[vert3D].v2 : map[vert3D].v1;
                new1Dvalues[i] = (vert1D, val3D);
            }
            raycastHits = new1Dvalues;
        }

        /// <summary>
        /// Requires deived classes to know how to receive one value to add onto each 1D vert index
        /// </summary>
        /// <param name="newValues"> List of 1D vert indices and values to add onto that index. </param>
        public abstract void Set1DValues((int, double)[] newValues);
        /* public abstract void SetPrev1DValues((int, double)[] newValues); */

        /// <summary>
        /// Requires derived classes to know how to make available one value for each 1D vertex
        /// </summary>
        /// <returns></returns>
        public abstract double[] Get1DValues ();
        /* public abstract double[] GetPrev1DValues (); */
        protected override void OnAwakePre()
        {
            UpdateGrid1D();

            // Add clamp manager
            GameObject clampManagerObj = Instantiate(GameManager.instance.clampManagerPrefab, transform);
            clampManager = clampManagerObj.GetComponent<NeuronClampManager>();

            base.OnAwakePre();
        }

        /// <summary>
        /// Read in the cell and initialize 3D/1D visualization/interaction infrastructure
        /// </summary>
        /// <returns> Unity Mesh visualization of the 3D geometry. </returns>
        /// <remarks> BuildVisualization is called by Simulation.cs,
        /// it is called after OnAwakePre and before OnAwakePost.
        /// If dryRun == true, Simulation will not call BuildVisualization. </remarks>
        protected override Mesh BuildVisualization () {
            if (!dryRun) {

                if (visualize1D) Render1DCell ();

                Update2DGrid();

                VisualMesh = Grid2D.Mesh;

                VisualMesh.Rescale(transform, new Vector3(4, 4, 4)); //TODO why 4?
                VisualMesh.RecalculateNormals();
                //TODO clean up this

                // Pass blownupMesh upwards to MeshSimulation
                ColliderMesh = VisualMesh;

                InitUI();
            }

            return VisualMesh;

            void Render1DCell () {
                Grid geom1D = Mapping.ModelGeometry;
                GameObject lines1D = gameObject.AddComponent<LinesRenderer> ().Draw (geom1D, color1D, lineWidth1D);
            }

            void InitUI()
            {
                GameObject gm = Instantiate(GameManager.instance.graphManagerPrefab, transform);
                graphManager = gm.GetComponent<NDGraphManager>();

                controlPanel = GameObject.FindGameObjectWithTag("ControlPanel");
                if(controlPanel == null)
                {
                    controlPanel = Resources.Load("Prefabs/NeuronalDynamics/ControlPanel/NDControls") as GameObject;
                    controlPanel = Instantiate(controlPanel);
                }

                NDBoardController controller = controlPanel.GetComponent<NDBoardController>();
                if (controller == null)
                {
                    Debug.LogWarning("No NDSimulationController found.");
                    Destroy(controlPanel);
                    return;
                }
                controller.MinimizeBoard(false);
            }
        }

        public void SwitchVisualMesh (double inflation) {
            inflation = Math.Clamp (inflation, 1, 5);
            VisualInflation = inflation;
        }

        private void UpdateGrid1D()
        {
            string meshName1D = VrnReader.Retrieve1DMeshName(RefinementLevel);
            /// Create empty grid with name of grid in archive
            Grid1D = new Grid(new Mesh(), meshName1D);
            Grid1D.Attach(new DiameterAttachment());

            VrnReader.ReadUGX(meshName1D, ref grid1D);

            Neuron = new Neuron(grid1D);
        }
        private void Update2DGrid()
        {
            /// Retrieve mesh names from archive
            string meshName2D = VrnReader.Retrieve2DMeshName(VisualInflation, RefinementLevel);

            /// Empty 2D grid which stores geometry + mapping data
            Grid2D = new Grid(new Mesh(), meshName2D);
            Grid2D.Attach(new MappingAttachment());
            VrnReader.ReadUGX(meshName2D, ref grid2D);
        }

        public int GetNearestPoint(RaycastHit hit)
        {
            if (mf == null) return -1;

            // Get 3D mesh vertices from hit triangle
            int triInd = hit.triangleIndex * 3;
            int v1 = mf.mesh.triangles[triInd];
            int v2 = mf.mesh.triangles[triInd + 1];
            int v3 = mf.mesh.triangles[triInd + 2];

            // Find 1D verts belonging to these 3D verts
            int[] verts1D = new int[]
            {
                Map[v1].v1, Map[v1].v2,
                Map[v2].v1, Map[v2].v2,
                Map[v3].v1, Map[v3].v2
            };
            Vector3 localHitPoint = transform.InverseTransformPoint(hit.point);

            float nearestDist = float.PositiveInfinity;
            int nearestVert1D = -1;
            foreach (int vert in verts1D)
            {
                float dist = Vector3.Distance(localHitPoint, Verts1D[vert]);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestVert1D = vert;
                }
            }

            return nearestVert1D;
        }
    }

    /// <summary>
    /// Stores two 1D indices and a lambda value for a 3D vertex
    /// </summary>
    /// <remarks>
    /// Lambda is a value between 0 and 1. A lambda value greater than 0.5 implies that the 3D vert lies closer to v2.
    /// A lambda value of 0 would imply that the 3D vert lies directly over v1,
    /// and a lambda of 1 implies that it lies completely over v2.
    /// </remarks>
    public struct Vert3D1DPair
    {
        public int v1 { get; private set; }
        public int v2 { get; private set; }
        public double lambda { get; private set; }

        public Vert3D1DPair(int v1, int v2, double lambda)
        {
            this.v1 = v1;
            this.v2 = v2;
            this.lambda = lambda;
        }
        public override string ToString()
        {
            return "v1: " + v1 + "\nv2: " + v2 + "\nlambda: " + lambda;
        }
    }
}
