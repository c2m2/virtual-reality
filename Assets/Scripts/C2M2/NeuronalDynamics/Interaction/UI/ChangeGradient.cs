using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using C2M2.Utils;
using TMPro;
using UnityEngine.UI;

namespace C2M2.NeuronalDynamics.Interaction.UI
{
    /// <summary>
    /// Can change the color scale of a simulation, as well as read in gradients from text files
    /// </summary>
    /// <remarks>
    /// The user can manually provide gradients and their names. This script will add any read 
    /// gradients to the existing list of gradients/names
    /// </remarks>
    public class ChangeGradient : MonoBehaviour
    {
        public GradientDisplay display = null;
        public TextMeshProUGUI nameDisplay = null;
        public Gradient[] gradients;
        public string[] gradientNames;
        public string defaultGradient;
        private int activeGrad = 0;

        public NDBoardController simController = null;

        private Color defaultCol = new Color(1f, 0.75f, 0f);
        public Color DefaultCol
        {
            get { return (simController == null) ? defaultCol : simController.defaultCol; }
        }
        private Color hoverCol = new Color(1f, 0.85f, 0.4f);
        public Color HoverCol
        {
            get { return (simController == null) ? hoverCol : simController.highlightCol; }
        }

        private string basePath = Application.streamingAssetsPath;
        public string extension = ".txt";
        public string subPath = Path.DirectorySeparatorChar + "Gradients";

        public void NextGrad()
        {
            if (activeGrad + 1 < gradients.Length) activeGrad++;
            else activeGrad = 0; // Wrap around the array

            ApplyGrad();
        }
        public void PrevGrad()
        {
            if (activeGrad - 1 >= 0) activeGrad--;
            else activeGrad = gradients.Length - 1; // Wrap around the array

            ApplyGrad();
        }

        public void ApplyGrad()
        {
            display.ColorLUT.Gradient = gradients[activeGrad];
            if (nameDisplay != null && gradientNames.Length == gradients.Length)
            {
                nameDisplay.text = gradientNames[activeGrad];
            }
            else Debug.LogWarning("Could not update gradient name.");

            display.ColorLUT.HasChanged = true;
        }

        private void Awake()
        {
            NullChecks();

            ReadGradients();

            void NullChecks()
            {
                if (display == null)
                {
                    display = GetComponentInParent<GradientDisplay>();
                    if (display == null)
                    {
                        Destroy(this);
                        Debug.LogError("No GradientDisplay given.");
                    }
                }
                if (simController == null)
                {
                    simController = GetComponentInParent<NDBoardController>();
                    if (simController == null)
                    {
                        Debug.LogWarning("No sim controller found. Text colors might be wrong.");
                    }
                }
            }
        }

        private void Start()
        {
            // Active gradient is 0 by default
            activeGrad = 0;
            for(int i = 0; i < gradientNames.Length; i++)
            {
                if (gradientNames[i].Equals(defaultGradient))
                {
                    activeGrad = i;
                }
            }

            // ApplyGrad();

            // set the gradient when loading from file
            Menu m = FindObjectOfType<Menu>();
            if (m.loading)
            {
                while (!m.finishedLoading)
                    System.Threading.Thread.Sleep(1);
                activeGrad = m.gradientIndex;
                ApplyGrad();
            }
            else
                ApplyGrad();
        }

        private void ReadGradients()
        {
            if(Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.LinuxPlayer || Application.platform == RuntimePlatform.LinuxEditor)
            {
                subPath = Path.AltDirectorySeparatorChar + "Gradients";

            }
            // Find gradient files in gradient directory
            DirectoryInfo d = new DirectoryInfo(basePath + subPath + Path.DirectorySeparatorChar);
            if(Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.LinuxPlayer || Application.platform == RuntimePlatform.LinuxEditor)
            {
                d = new DirectoryInfo(basePath + subPath + Path.AltDirectorySeparatorChar);
            }
            FileInfo[] files = d.GetFiles("*" + extension);

            if (files.Length > 0)
            {
                // Get only the name of the gradient from each found file
                string[] readNames = new string[files.Length];
                for (int i = 0; i < files.Length; i++)
                {
                    string name = Path.GetFileName(files[i].Name);
                    readNames[i] = name.Remove(name.Length - extension.Length);
                }

                Gradient[] readGrads = new Gradient[readNames.Length];

                // Read requested gradietns
                for(int i = 0; i < readNames.Length; i++)
                {
                     if(Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.LinuxPlayer || Application.platform == RuntimePlatform.LinuxEditor)
                    {
                        readGrads[i] = ReadGradient.Read(basePath + subPath + Path.AltDirectorySeparatorChar + readNames[i] + extension);
                    }
                    else
                    {
                        readGrads[i] = ReadGradient.Read(basePath + subPath + Path.DirectorySeparatorChar + readNames[i] + extension);
                    }
                }

                // Merge read gradients and manually given gradients
                int totalGradients = readGrads.Length + gradients.Length;
                Gradient[] gradsTemp = new Gradient[totalGradients];
                string[] namesTemp = new string[totalGradients];
                for (int i = 0; i < gradients.Length; i++)
                {
                    gradsTemp[i] = gradients[i];
                    namesTemp[i] = (i < gradientNames.Length) ? gradientNames[i] : "";
                }
                for(int i = gradients.Length; i < totalGradients; i++)
                {
                    gradsTemp[i] = readGrads[i - gradients.Length];
                    namesTemp[i] = readNames[i - gradients.Length];
                }

                gradients = gradsTemp;
                gradientNames = namesTemp;
            }
        }

        public void SetDefaultCol(Image img)
        {
            img.color = DefaultCol;
        }

        public void SetHighlightCol(Image img)
        {
            img.color = HoverCol;
        }

        // for saving/loading purposes
        public int GetActiveGradient() { return activeGrad; }
        public void SetActiveGradient(int grad) { activeGrad = grad; }
    }
}
