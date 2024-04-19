using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using C2M2.Utils;
using TMPro;
using UnityEngine.UI;
namespace C2M2.NeuronalDynamics.Interaction.UI
{
    [RequireComponent(typeof(BoxCollider))]
    public class NDExtremaController : MonoBehaviour
    {
        public TextMarker tm = null;
        public RectTransform increaseButton = null;
        public RectTransform decreaseButton = null;
        public RectTransform resetButton = null;
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
        private Color pressCol = new Color(1f, 0.9f, 0.6f);
        public Color PressedCol
        {
            get { return (simController == null) ? pressCol : simController.pressedCol; }
        }

        public float buttonSize = 25;
        public bool affectMax = true;
        public float shiftSensivitivty = 100f;
        public bool latchToInt = true;

        public GradientDisplay GradDisplay
        {
            get
            {
                return tm.gradDisplay;
            }
        }
        public TextMeshProUGUI Label
        {
            get
            {
                return tm.label;
            }
            set
            {
                tm.label = value;
            }
        }

        private BoxCollider bc;

        private float GlobalMax
        {
            get
            {
                return GradDisplay.ColorLUT.GlobalMax;
            }
            set
            {
                if (value > GlobalMin)
                {
                    GradDisplay.ColorLUT.GlobalMax = value;
                }
            }
        }
        private float GlobalMin
        {
            get { return GradDisplay.ColorLUT.GlobalMin; }
            set
            {
                if (value < GlobalMax)
                {
                    GradDisplay.ColorLUT.GlobalMin = value;
                }
            }
        }

        private float fi = -1;
        private float startTime = 0;
        private float maxHoldTime = 5.0f;
        private float ff = -1;

        private float ThresholdScaler = 1;
        public float Threshold
        {
            get
            {
                return ThresholdScaler * (1 / GradDisplay.UnitScaler);
            }
        }

        public OVRInput.Axis2D thumbstickP = OVRInput.Axis2D.PrimaryThumbstick;
        public OVRInput.Axis2D thumbstickS = OVRInput.Axis2D.SecondaryThumbstick;
        public KeyCode incKey = KeyCode.UpArrow;
        public KeyCode decKey = KeyCode.DownArrow;
        public float PowerModifier
        {
            get
            {
                // Uses the value of both joysticks added together
                if (GameManager.instance.vrDeviceManager != null && GameManager.instance.vrDeviceManager.VRActive) return OVRInput.Get(thumbstickP).y + OVRInput.Get(thumbstickS).y;
                else if (Input.GetKey(incKey)) return 1;
                else if (Input.GetKey(decKey)) return -1;
                else return 0f;
            }
        }

        private void Awake()
        {
            NullChecks();

            bc = GetComponent<BoxCollider>();
            gameObject.layer = LayerMask.NameToLayer("Raycast");

            void NullChecks()
            {
                bool fatal = false;

                if(simController == null)
                {
                    simController = GetComponentInParent<NDBoardController>();
                    if(simController == null)
                    {
                        Debug.LogWarning("Could not find simulation controller. Text colors could be wrong.");
                    }
                }
                if(increaseButton == null)
                {
                    Debug.LogError("No increase button given.");
                    fatal = true;
                }
                if(decreaseButton == null)
                {
                    Debug.LogError("No decrease button given.");
                    fatal = true;
                }
                if(resetButton == null)
                {
                    Debug.LogError("No reset button given.");
                    fatal = true;
                }

                if (fatal) Destroy(this);
            }
        }
        private void Start()
        {
            Label.transform.hasChanged = true;

            UpdateButtons();

            ResetScaler();
        }
        private void Update()
        {
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            if(buttonSize != Label.bounds.size.y)
            {
                buttonSize = Label.bounds.size.y;
                ResizeButtons();
            }

            if (Label.transform.hasChanged)
            {
                RepositionButtons();
                Label.transform.hasChanged = false;
            }

            void ResizeButtons()
            {
                Image[] buttons = GetComponentsInChildren<Image>();
                foreach (Image b in buttons)
                {
                    Vector3 size = b.rectTransform.sizeDelta;
                    b.transform.localScale = new Vector3(buttonSize / size.x, buttonSize / size.y, 1f);
                }
                BoxCollider[] cols = GetComponentsInChildren<BoxCollider>();
                foreach (BoxCollider b in cols)
                {
                    b.size = new Vector3(buttonSize, buttonSize, 1f);
                }
            }
            void RepositionButtons()
            {
                // Center buttons on label
                transform.localPosition = new Vector3(Label.transform.localPosition.x, Label.transform.localPosition.y, Label.transform.localPosition.z);
                float labelHeight = Label.bounds.extents.y;
                float labelWidth = Label.bounds.extents.x;
                increaseButton.localPosition = new Vector3(Label.bounds.max.y + buttonSize, 0f, 0f);
                decreaseButton.localPosition = new Vector3(Label.bounds.min.y - buttonSize, 0f, 0f);
                resetButton.localPosition = new Vector3(0f, Label.bounds.extents.x + buttonSize, 0f);

                bc.size = new Vector3(labelHeight*2 + buttonSize, labelWidth*2 + buttonSize, 1f);
            }
        }

        private void ScaleExtremaPress(float sign)
        {
            float pressAmt = 2 * (GlobalMax - GlobalMin) / shiftSensivitivty;
            SetExtrema(sign * pressAmt);

            UpdateButtons();

            startTime = Time.unscaledTime;
        }
        public void IncreaseExtremaPress() => ScaleExtremaPress(1);
        public void DecreaseExtremaPress() => ScaleExtremaPress(-1);

        public void ScaleExtremaPress(RaycastHit hit)
        {
            startTime = Time.unscaledTime;
        }
        public void ScaleExtremaHold(RaycastHit hit)
        {
            ScaleExtremaHold(PowerModifier);
        }
        private void ScaleExtremaHold(float sign)
        {
            if (sign == 0) return;

            float holdTime = Time.unscaledTime - startTime;

            SetExtrema(sign * Time.fixedDeltaTime * GetScaler(Math.Min(holdTime, maxHoldTime)));

            UpdateButtons();
        }
        public void IncreaseExtremaHold() => ScaleExtremaHold(1);
        public void DecreaseExtremaHold() => ScaleExtremaHold(-1);

        private void SetExtrema(float val)
        {
            // Clamp the value so that GlobalMin and GlobalMax can't get within a threshold of eachother
            if (affectMax)
                GlobalMax = (GlobalMax + val >= GlobalMin + Threshold) ? 
                    (GlobalMax + val) : 
                    (GlobalMax = GlobalMin + Threshold);
            else
                GlobalMin = (GlobalMin + val <= GlobalMax - Threshold) ? 
                    (GlobalMin + val) : 
                    (GlobalMax - Threshold);
        }
        public void ResetExtrema()
        {
            if (affectMax) GlobalMax = GradDisplay.originalMax;
            else GlobalMin = GradDisplay.originalMin;
        }

        public void ResetScaler()
        {
            if (latchToInt)
            {
                LatchToInt();
            }

            startTime = float.NegativeInfinity;
            fi = 0;
            ff = GlobalMax - GlobalMin;
        }
        private void LatchToInt()
        {
            // If we operate from 0.000 to 0.055, we cannot round to an int.
            // We need to convert to the display value (0 to 55), round, and then convert back
            float val = affectMax ? GlobalMax : GlobalMin;
            val = Mathf.Round(val * GradDisplay.UnitScaler) / GradDisplay.UnitScaler;

            if (affectMax)
            {
                // Don't let max round to min value
                if (val == GlobalMin) val = ((val * GradDisplay.UnitScaler) + 1) / GradDisplay.UnitScaler;
                GlobalMax = val;
            }
            else
            {
                // Don't let min round to max value
                if (val == GlobalMax) val = ((val * GradDisplay.UnitScaler) - 1) / GradDisplay.UnitScaler;
                GlobalMin = val;
            }
        }

        // fi = 2 * (Max - Min) / sensistivity
        // ff = (Max - Min)
        // f(x) = ((ff - fi) / maxHoldTime) * x + fi
        private float GetScaler(float holdTime) => ((ff - fi) / maxHoldTime) * holdTime + fi;

        public void LabelColDefault() => ChangeLabelCol(DefaultCol);
        public void LabelColHover() => ChangeLabelCol(HoverCol);
        public void LabelColPress() => ChangeLabelCol(PressedCol);
        private void ChangeLabelCol(Color col)
        {
            Label.color = col;
        }
    }
}
