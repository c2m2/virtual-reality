using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.Events;

namespace C2M2 {
    public class ButtonTest : MonoBehaviour {
        private void OnMouseUpAsButton(){
            Debug.Log("Button works");
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);

        }
    }
}

