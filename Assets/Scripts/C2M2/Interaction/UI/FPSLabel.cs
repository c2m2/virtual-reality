using System;
using UnityEngine;
using TMPro;
namespace C2M2.Interaction.UI
{
    /// <summary>
    /// Stores and updates text labels for FPS readings
    /// </summary>
    public class FPSLabel : MonoBehaviour
    {
        public TextMeshProUGUI highFPSReading;
        public TextMeshProUGUI avgFPSReading;
        public TextMeshProUGUI lowFPSReading;
        private Utils.DebugUtils.FPSCounter fpsCounter;

        void Start()
        {
            fpsCounter = GameManager.instance.fpsCounter;
            if (avgFPSReading == null) throw new LabelNotFoundException();
            if (highFPSReading == null) throw new LabelNotFoundException();
            if (lowFPSReading == null) throw new LabelNotFoundException();
        }

        void Update()
        {
            avgFPSReading.text = fpsCounter.avgStr;
            highFPSReading.text = fpsCounter.highStr;
            lowFPSReading.text = fpsCounter.lowStr;
        }
        public class LabelNotFoundException : Exception
        {
            public LabelNotFoundException() { }
            public LabelNotFoundException(string message) : base(message) { }
            public LabelNotFoundException(string message, Exception inner) : base(message, inner) { }
        }
    }
}