using UnityEngine;
using C2M2.Utils;

namespace C2M2.Interaction
{
    /// <summary>
    /// Controls the scaling of a transform
    /// </summary>
    [RequireComponent(typeof(OVRGrabbable))]
    public class GrabRescaler : MonoBehaviour
    {
        private OVRGrabbable grabbable = null;
        private Vector3 origScale;
        private Vector3 minScale;
        private Vector3 maxScale;
        public float scaler = 50f;
        public float minPercentage = 0;
        public float maxPercentage = float.PositiveInfinity;
        public bool xScale = true;
        public bool yScale = true;
        public bool zScale = true;
        public OVRInput.Button vrThumbstick = OVRInput.Button.PrimaryThumbstick;
        public OVRInput.Button vrThumbstickS = OVRInput.Button.SecondaryThumbstick;
        public KeyCode incKey = KeyCode.UpArrow;
        public KeyCode decKey = KeyCode.DownArrow;
        public KeyCode resetKey = KeyCode.R;
        public Transform target = null;

        private float ChangeScaler
        {
            get
            {
                ///<returns>A float between -1 and 1, where -1 means the thumbstick y axis is completely down and 1 implies it is all the way up</returns>
                if (GameManager.instance.vrDeviceManager.VRActive) return (OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick).y + OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick).y);
                else if (Input.GetKey(incKey) && !Input.GetKey(decKey)) return .2f;
                else if (Input.GetKey(decKey) && !Input.GetKey(incKey)) return -.2f;
                return 0;
            }
        }


        ///<returns>A boolean of whether the joystick is pressed</returns>
        private bool ResetPressed
        {
            get
            {
                if (GameManager.instance.vrDeviceManager.VRActive) return (OVRInput.Get(vrThumbstick) || OVRInput.Get(vrThumbstickS));
                else return Input.GetKey(resetKey);
            }
        }

        private void Start()
        {
            if (target == null) target = transform;

            grabbable = GetComponent<OVRGrabbable>();

            // Use this to determine how to scale at runtime
            origScale = target.localScale;
            minScale = minPercentage * origScale;
            if (maxPercentage == float.PositiveInfinity) maxScale = Vector3.positiveInfinity;
            else maxScale = maxPercentage * origScale;
        }

        void Update()
        {
            // RaycastEventHandler handles calling rescale for Desktop mode, TODO this is bad and should be changed
            if (GameManager.instance.vrDeviceManager == null || !GameManager.instance.vrDeviceManager.VRActive) return;

            if (grabbable.isGrabbed) Rescale();
        }

        public void Rescale()
        {
            if (ResetPressed)
            {
                target.localScale = origScale;
            }
            else if(ChangeScaler != 0)
            {
                Vector3 scaleValue = scaler * Time.deltaTime * ChangeScaler * origScale;
                Vector3 newLocalScale = target.localScale + scaleValue;

                // Makes sure the new scale is within the determined range
                newLocalScale = Math.Clamp(newLocalScale, minScale, maxScale);

                // Only scales the proper dimensions
                if (!xScale) newLocalScale.x = target.localScale.x;
                if (!yScale) newLocalScale.y = target.localScale.y;
                if (!zScale) newLocalScale.z = target.localScale.z;

                target.localScale = newLocalScale;
            }
        }
    }
}
