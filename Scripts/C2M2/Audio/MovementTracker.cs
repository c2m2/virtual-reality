using C2M2.Utils.Editor;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*

What I wanted to do with this script:
    Attach this script to the Center Eye Anchor gameobject itself in the Editor Heirarchy as a component using "Add Component".
    That would in theory, allow me to reference the 3d VR Player's position, which is equal to, "CenterEyeAnchor.transform.position".
    I would then be able to save that position to a Vector3 variable called "position" in this class.
    
    However, when instantiating this class as an object in VertexAudio, and referencing "position" as it's saved in the Start() Method, there's no console output.
    I print out the "position" variable in the Update() function of Vertex Audio, move around in VR, and there's no Debug.Log() console output.
    
    I am not sure why this is the case.
    It may be: Human Error or a VR/Unity error.
    
 */


public class MovementTracker : MonoBehaviour
{

    Vector3 position;
    void Start()
    {

        position = this.transform.position;
    }

    void Update()
    {

    }

    public Vector3 GetPosition3d()
    {
        return position;
    }


}
