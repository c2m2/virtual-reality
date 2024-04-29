#region includes
using System;
using System.IO;
using C2M2.NeuronalDynamics.UGX;
using UnityEngine;
using Grid = C2M2.NeuronalDynamics.UGX.Grid;
#endregion

namespace C2M2.NeuronalDynamics.Visualization.VRN {
    using DiameterAttachment = IAttachment<DiameterData>;
        

    /// VRNREADERBEHAVIOUR
    /// <summary>
    /// Example behaviour to demonstrate vrnReader usage
    /// </summary>
    public class VrnReaderBehaviour : MonoBehaviour {
        /// the test archive file test.vrn
        public string fileName = "testNew.vrn";

        /// <summary>
        /// On start a test file (test.vrn) is read from the assets folder
        /// </summary>
        /// The coarse 1d mesh is retrieved as well as the by a factor of 
        /// 2.5 inflated 2d mesh is retrieved from the archive test.vrn
        public void start () {

            try {
                ////////////////////////////////////////////////////////////////
                /// Example 1: List grids and/or retrieve mesh file name from 
                ///            archive based on refinement or inflation
                ////////////////////////////////////////////////////////////////
                string fullFileName = "C:/Users\bhugg/Documents/GitHub/Neuro-VISOR/Assets/Resources/Geometries";
                Debug.Log(fullFileName);
                /// Instantiate the VRN reader with the desired file name (.vrn archive) to load from Assets
               VrnReader reader = new VrnReader (fullFileName);
                /// List all 1D and 2D geometries contained in given .vrn archive
                Debug.Log (reader.List ());

                /// Get the name of the 1d mesh (0-th refinement aka coarse grid) in archive
                UnityEngine.Debug.Log (reader.Retrieve1DMeshName ());
                /// Get the name of the inflated 2d mesh by a factor of 2.5 in archive
                UnityEngine.Debug.Log (reader.Retrieve2DMeshName (2.5));
                ////////////////////////////////////////////////////////////////

                string meshName1D = reader.Retrieve1DMeshName ();
                /// Create empty grid with name of grid in archive
                Grid grid1D = new Grid (new Mesh (), meshName1D);
                grid1D.Attach (new DiameterAttachment ());
                /// Read in the .ugx file into the grid (read_ugx uses UGXReader internally)
                UnityEngine.Debug.Log ("Reading now mesh: " + meshName1D);
                reader.ReadUGX (meshName1D, ref grid1D);
                ////////////////////////////////////////////////////////////////
                /// Example 2: Load a UGX file (mesh) from the .vrn archive and 
                /// store it in a Grid object: Here the 1D coarse grid is loaded
                ////////////////////////////////////////////////////////////////
                /// NOTE: If the required refinement or inflation is not stored in the archive the first available 1D or 2D mesh is loaded
                /// 1. Find a refinement in the .vrn archive (0 = 0th refinement = coarse grid, 1 = 1st refinement, 2 = 2nd refinement, ...)
                /// or: Find a inflated mesh in the .vrn archive (1 = Inflated by factor 1, 2.5 = inflated by a factor 2.5, ...)
                /// 2. Create empty Grid grid to store the mesh
                /// 3. Read the file from the archive (.ugx filetype) into the Grid grid
                string meshName2D = reader.Retrieve2DMeshName ();
                /// Create empty grid with name of grid in archive
                Grid grid2D = new Grid (new Mesh (), meshName2D);
                grid2D.Attach (new DiameterAttachment ());
                /// Read in the .ugx file into the grid (read_ugx uses UGXReader internally)
                UnityEngine.Debug.Log ("Reading now mesh: " + meshName2D);
                reader.ReadUGX (meshName2D, ref grid2D);

                GetComponent<MeshFilter> ().sharedMesh = grid2D.Mesh;
                ////////////////////////////////////////////////////////////////
            } catch (Exception ex) when (ex is System.IO.FileNotFoundException || ex is System.ArgumentNullException) {
                UnityEngine.Debug.LogError ($"Archive not found or unable to open the .vrn archive: {fileName}.");
                UnityEngine.Debug.LogError (ex);

            } catch (Exception ex) when (ex is CouldNotReadMeshFromVRNArchive) {
                UnityEngine.Debug.LogError ($"Requested mesh not contained in MetaInfo.json or given .vrn archive {fileName}.");
                UnityEngine.Debug.LogError (ex);
            }
        }
    }
}
