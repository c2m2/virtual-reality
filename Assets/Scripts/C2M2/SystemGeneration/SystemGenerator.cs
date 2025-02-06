/*
A very rough starting point for a function to automatically generate neuronal systems with procedural generation.
For now, neurons are represented as spherical nodes and synapses are represented as links. At its end state, 
this will be used directly in the simulation with a menu (seperate script). 

In its current state, this system generates i nodes and i-1 links in a pre-determined spherical area A. 
Essentially, it's a big snake. This is obviously not ideal, and will be programmed out.

TODO:
    ESSENTIALS:
    - Varying number of synapses on each neuron. Maybe a maximum number of synapses per neuron could be implemented 
    as a parameter.

    - Neurons should not be allowed to overlap with eachother. Maybe they could each have a radius that is 'off 
    limits' to other neurons? This could cause a problem with potential overflow; Where will the neurons go if 
    they can't overlap but also can't leave the initial area A? We could potentially have the max neurons adjust 
    relative to the max area: 
        n = # of neurons, R = radius of initial area A, r = radius of 'off limits' space around each neuron
        n * (pi * r^2) < (pi * R^2)
        so 
        n < (R^2 / r^2)
        NOTE: this does not account for the space between each r in A. Nor does it account for nodes potentially
        not being placed optimally. Maybe we can approximate this by dividing (R^2 / r^2) by 2?
    Or adjust the maximum radius of the initial area A relative to the number of neurons:
        n * (pi * r^2) < (pi * R^2)
        so
        sqrt(n) * r < R
        NOTE: same problem as above. for now, dividing R by 2 is okay. If we actually use this method we should
        probably find a better approximation

    - Replacing nodes w/ neurons and links w/ synapses
    
    AESTHETICS:
    - Neurons and synapses generating in a visibly sequential way (in order of closest to farthest)
*/

using UnityEngine;
using System.Collections.Generic;

namespace C2M2 {
    public class SystemGenerator : MonoBehaviour {
        /* PROPERTIES *********************************************************************************************/
        public int numberOfNodes = 10;
        public float radiusOfMaxArea = 5f;      // Radius of the spherical area the nodes are allowed to occupy
        public GameObject spherePrefab;
        public GameObject arrowPrefab;
        private List<GameObject> nodes = new List<GameObject>();
        /*********************************************************************************************************/
        void Start() {
            GenerateNodes();
            ConnectNodes();
        }

        /*
        Generates n nodes and then assigns them random positions within the area sphere.
        */
        void GenerateNodes() {
            for (int i = 0; i < numberOfNodes; i++) {
                // Make node
                GameObject node = GameObject.CreatePrimitive(PrimitiveType.Sphere);

                // Create and assign position
                Vector3 position = Random.insideUnitSphere * radiusOfMaxArea;
                node.transform.position = position;

                // Reduce size (default is huge)
                node.transform.localScale = Vector3.one * 0.5f; 

                // Add it to the list
                nodes.Add(node);
            }
        }

        /*
        Connects all nodes sequentially. This is bad and is first on the agenda to change
        */
        void ConnectNodes() {
            for (int i = 0; i < nodes.Count - 1; i++) {
                DrawLink(nodes[i].transform.position, nodes[i + 1].transform.position);
            }
        }

        /*
        Draws a link between two points on a 3D plane (Vector3 objects)
        */
        void DrawLink(Vector3 start, Vector3 end) {
            GameObject link = GameObject.CreatePrimitive(PrimitiveType.Cylinder);

            // Vector representing travel from start and end
            Vector3 direction = end - start;
            float distance = direction.magnitude;

            // Position link at the midpoint between start and end
            link.transform.position = (start + end) / 2; 

            // Transforms the cylinder's local "up" angle to align with direction
            link.transform.up = direction.normalized; 

            // y scale is set to distance / 2 because the origin is in the center of the cylinder
            link.transform.localScale = new Vector3(0.1f, distance / 2, 0.1f); // Adjust size
        }
    }
}