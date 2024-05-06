using System.Collections.Generic;
using UnityEngine;
using System;
using System.Collections;
using C2M2.Interaction;
using System.Security.Claims;
using System.Net;
using CSparse.Ordering;
using MathNet.Numerics.Statistics;
using UnityEngine.Audio;
using C2M2.NeuronalDynamics.Simulation;
using C2M2.NeuronalDynamics.UGX;
using static Unity.Burst.Intrinsics.X86.Avx;
using Grid = C2M2.NeuronalDynamics.UGX.Grid;
using C2M2.Utils;
using UnityEngine.Networking.Types;
using System.Runtime.CompilerServices;
using UnityEngine.Experimental.AI;
using Oculus.Spatializer.Propagation;
using System.Data.Common;
using System.Data.SqlClient;
using C2M2.NeuronalDynamics.Interaction.UI;
using JetBrains.Annotations;
using static OVRInput;
using System.Linq;

/*
    THIS SCRIPT = Dynamic Spatialized Audio;However, the voltage values are NOT mapped using actual mathematics, so this version is NOT ideal
   
NEXT STEPS
(1)Maintain a reference to the Virtual Reality user 3d Vector3 position
- Using this reference, we can Mathematically spatialize the Audio Sources(plots) based on Vector3 coordinates of the User vs Audio Source
(2) Dynamic Audio Coroutine is called thousands of times(update function), leading to thousands of AudioClips being created, which may be a factor causing unpleasent output audio
(3) Smaller Neuron voltage shifts, produce audio shifts that are virtually impossible to distinguish. Figure out relationship, if any, between vertex count & audio
 */

namespace C2M2.NeuronalDynamics.Interaction
{
    [RequireComponent(typeof(AudioSource))]
    public class VertexAudio : MonoBehaviour
    {
        SparseSolverTestv1 sparsecomponent;
        NDGraph audioassociatedplot;
        private double[] store_volts_realtime;
        private float samplerate = 48100;
        AudioSource audioref;
        
        private float frequencyMin = 150f;
        private float frequencyMax = 300f;
        private double VoltageMin = -0.02;
        private double VoltageMax = 0.03;
        private double phase = 0;
        
        private void Awake()
        {
            audioassociatedplot = GetComponent<NDGraph>();
        }


        private void Start()
        {
            audioref = GetComponent<AudioSource>();
            var vraudio = this.gameObject.AddComponent<ONSPAudioSource>();
        }

        private void Update()
        {
            StartCoroutine(DynamicAudioVolts(audioassociatedplot, sparsecomponent));
        }

        /* These 2 Functions utilize Unity AudioSource & Oculus VR Plugin Audio Source properties that deal with Spatialized Audio
     * What to Change/Fix : "Near/Far" Math Equations should relate to the User's VR Position(3d) and the Position(3d) of the AudioSource(plot)
     * (1) Need to find a way to retain a reference to the VR Player's position
     * (2) Most obvious equation if Step 1 was to be completed, is to utilize Vector3.distance(a,b), where the player is (a) & the source is (b) or vice versa
     */

        public void SetONSPAudioProperties(ONSPAudioSource source)
        {
            //Z = Horizontal Movement in Room & X = Forward and Backward Movement in Room
            source.VolumetricRadius = 0;
            source.Gain = 1f;
            source.Near = 1 - (source.gameObject.transform.position.z + source.gameObject.transform.position.x) / 2;
            source.Far = 1 - (source.gameObject.transform.position.z + source.gameObject.transform.position.x) / 2;
            source.EnableSpatialization = true;
            source.UseInvSqr = true;
        }
        private void SetAudioSourceProperties(AudioSource source)
        {
            source.loop = false;
            source.volume = 1f;
            source.spatialBlend = 1;
        }

        /* Produces Dynamic Audio based on the clicked Plot Vertex in the simulation */
        private IEnumerator DynamicAudioVolts(NDGraph plot, SparseSolverTestv1 neuron)
        {
            /*Each Instance of a plot is connected to a Neuron. Store : Plot Vertex,Neuron Vertex Count, Real-time voltages of the neuron*/

            AudioSource src = GetComponent<AudioSource>();
            ONSPAudioSource vrsource = GetComponent<ONSPAudioSource>();
            neuron = plot.simulation.gameObject.GetComponent<SparseSolverTestv1>();
            double[] volts = neuron.Get1DValues();
            float frequency;
            /*Utilizes thresholds to alter audio source properties like volume/pitch to simulate dynamic audio. This audio IS Spatialized somehow. */
            for (int i = 0; i < volts.Length; i += 2)
            {
                if (volts[i] < 0)
                {
                    frequency = 50f;
                    src.volume = 0.25f;
                    src.pitch = 0.25f;
                }
                else
                {
                    frequency = 350f;
                  
                }
                phase += 2 * Mathf.PI * (frequency / samplerate);
                volts[i] = Mathf.Sin((float)phase);
            }
         /* Audio Clip created on every call to this Coroutine, leading to thousands of clips created.Not ideal. Ideally Create 1 clip(in start() ),
          * whos clip data is altered at runtime */

            AudioClip clip = AudioClip.Create("DynamicAudio", (int)(samplerate * 2), 1, (int)samplerate, false);
            float[] real_timevoltsf = C2M2.Utils.Array.ToFloat(volts);
            clip.SetData(real_timevoltsf, 0);
            SetAudioSourceProperties(src);
            SetONSPAudioProperties(vrsource);
            src.clip = clip;

            while (true)
            {
                src.Play();
                yield return null;
            }

        }

        /* 2 functions below show the identical code structure for plots/clamps. Meaning, audio can be produced for any object placed on a vertex*/

        public void plotVertexVolts(NDGraph plot, SparseSolverTestv1 neuron)
        {
            neuron = plot.simulation.gameObject.GetComponent<SparseSolverTestv1>();
            print("plotvertexvolts neuron name = " + neuron.name);
            Vector3[] storeVerts = neuron.Verts1D;
            double[] realTimeVolts = neuron.Get1DValues();
            int vertClicked = plotFocusVert(plot);

            for (int i = 0; i < storeVerts.Length; i++)
            {
                float xPos = storeVerts[i].x;
                float yPos = storeVerts[i].y;
                float zPos = storeVerts[i].z;
                Vector3 condensedVertPos = new Vector3((float)xPos, (float)yPos, (float)zPos);
                if (condensedVertPos == plotWorldSpacePos(plot))
                {
                    for (int j = 0; j < realTimeVolts.Length; j++)
                    {
                        print("Vertex Position in World Space = " + condensedVertPos);
                        print("VOLTS at VERTEX= " + realTimeVolts[j]);
                        print("MATCH VOLTAGES ABOVE? = " + realTimeVolts[vertClicked]);
                        print("VOLTS AT VERTEX " + vertClicked + ": = " + realTimeVolts.GetValue(vertClicked));
                    }
                }
            }

        }

        public int voltageAtClampVertexLocation(AudioSource clamp, SparseSolverTestv1 neuron)
        {
            neuron = clamp.GetComponentInParent<SparseSolverTestv1>();
            Vector3[] storeVerts = neuron.Verts1D;
            double[] realTimeVolts = neuron.Get1DValues();
            int vertClicked = clampFocusVert(clamp);

            for (int i = 0; i < storeVerts.Length; i++)
            {
                double xPos = storeVerts[i].x;
                double yPos = storeVerts[i].y;
                double zPos = storeVerts[i].z;
                Vector3 condensedVertPos = new Vector3((float)xPos, (float)yPos, (float)zPos);
                if (condensedVertPos == clampWorldSpacePos(clamp))
                {
                    for (int j = 0; j < realTimeVolts.Length; j++)
                    {
                        print("Vertex Position in World Space = " + condensedVertPos);
                        print("VOLTS at VERTEX= " + realTimeVolts[j]);
                        print("MATCHES VOLTAGES ABOVE = " + realTimeVolts[vertClicked]);
                        print("GET VALUE AT INDEX CLICKED = " + realTimeVolts.GetValue(vertClicked));
                    }

                }
            }
            return vertClicked;
        }

        #region Getters
        private Vector3 plotWorldSpacePos(NDGraph sim_plot)
        {
            return sim_plot.FocusPos;
        }
        private int plotFocusVert(NDGraph sim_plot)
        {
            return sim_plot.FocusVert;
        }

        private Vector3 clampWorldSpacePos(AudioSource clamp)
        {
            return clamp.GetComponentInParent<NeuronClamp>().FocusPos;
        }
        private Vector3 vrclampWorldSpacePos(ONSPAudioSource clamp)
        {
            return clamp.GetComponentInParent<NeuronClamp>().FocusPos;
        }
        private int clampFocusVert(AudioSource clamp)
        {
            return clamp.GetComponentInParent<NeuronClamp>().FocusVert;
        }

        #endregion

    }

}
