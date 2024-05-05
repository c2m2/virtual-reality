using System.Collections.Generic;
using UnityEngine;
using System;
using System.Collections;
using UnityEngine.Audio;
using C2M2.NeuronalDynamics.Simulation;
using C2M2.NeuronalDynamics.UGX;
using C2M2.Utils;
using Oculus.Spatializer.Propagation;
using C2M2.NeuronalDynamics.Interaction;
using C2M2;

/*
    THIS SCRIPT = Dynamic Audio, NOT Spatialized
   
NEXT STEPS
(1)Maintain a reference to the Virtual Reality user 3d Vector3 position
- Using this reference, we can Mathematically spatialize the Audio Sources(plots) based on Vector3 coordinates of the User vs Audio Source
(2) Dynamic Audio Coroutine is called thousands of times(update function), leading to thousands of AudioClips being created, which may be a factor causing unpleasent output audio
(3) Smaller Neuron voltage shifts, produce audio shifts that are virtually impossible to distinguish. Figure out relationship, if any, between vertex count & audio
 */

[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(ONSPAudioSource))]
public class VertexAudio: MonoBehaviour
{
    SparseSolverTestv1 sparsecomponent;
    NDGraph audioassociatedplot;
    private float samplerate = 48100;//universal standard samplerate
    AudioSource audioref;
 
    private float frequencyMin = 440f;
    private float frequencyMax = 800f;
    private double VoltageMin = -0.02;
    private double VoltageMax = 0.05;
    private double frequency, voltageCapped;
    private double phase = 0;

    private static int count_times_audio_clip_created = 0;

    /* Each plot instance holds a copy of this script that allows the plot to produce audio */
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
        source.VolumetricRadius = 0;//For "Point" Audio Sources, the value 0 is recommended. Stated in documentation for the Oculus VR Plugin
        source.Gain = 1f;
        source.Near = 1 - (source.gameObject.transform.position.z - source.gameObject.transform.position.x) / 2;
        source.Far = 1 - (source.gameObject.transform.position.z - source.gameObject.transform.position.x) / 2;

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
        Vector3[] storeVerts = neuron.Verts1D;
        double[] volts = neuron.Get1DValues();
        int vertClicked = plotFocusVert(plot);

        /*Map incoming voltage from clicked vertex to a frequency : Minimum Freq <= frequency <= Max Frequency   */
        for (int i = 0; i < storeVerts.Length; i++)
        {
            float xPos = storeVerts[i].x;
            float yPos = storeVerts[i].y;
            float zPos = storeVerts[i].z;
            Vector3 condensedVertPos = new Vector3((float)xPos, (float)yPos, (float)zPos);
            if (condensedVertPos == plotWorldSpacePos(plot))
            {
                voltageCapped = Mathf.Min(Mathf.Max((float)volts[vertClicked], (float)VoltageMin), (float)VoltageMax);
                frequency = frequencyMin + (frequencyMax - frequencyMin) * ((voltageCapped - VoltageMin) / (VoltageMax - VoltageMin));
                phase += 2 * Mathf.PI * (frequency / samplerate);
                volts[vertClicked] = Mathf.Sin((float)phase);
            }
        }
        /*Set mapped frequencies as clip sample data. Sample data ranges from -1 to 1. Play audioclip while the plot is != null */
        AudioClip clip = AudioClip.Create("DynamicAudio", (int)(samplerate * 2), 1, (int)samplerate, false);
        float[] real_timevoltsf = C2M2.Utils.Array.ToFloat(volts);
        clip.SetData(real_timevoltsf, 0);
        SetAudioSourceProperties(src);
        SetONSPAudioProperties(vrsource);
        src.clip = clip;
        /* Audio Clip created on every call to this Coroutine, leading to thousands of clips created.Not ideal. Ideally Create 1 clip(in start() ), whos clip data is altered at runtime */
        //count_times_audio_clip_created++;

        while (src.isActiveAndEnabled)
        {
            src.Play();
            yield return new WaitForSeconds(2);
        }

    }

    /* 2 functions below show the identical code structure for plots/clamps. Meaning, audio can be produced for any object placed on a vertex*/
    public void VoltsPlotVertex(NDGraph plot, SparseSolverTestv1 neuron)
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
            if (condensedVertPos == plotWorldSpacePos(plot)){
                print("Vertex Position in World Space = " + condensedVertPos);
                print("Volts[VertexClicked] = " + realTimeVolts[vertClicked]);
                print("Volts.GetValue(VertClicked) = " + realTimeVolts.GetValue(vertClicked));
            }
        }

    }
    public int VoltsClampVertex(AudioSource clamp, SparseSolverTestv1 neuron)
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
            if (condensedVertPos == clampWorldSpacePos(clamp)){
                print("Vertex Position in World Space = " + condensedVertPos);
                print("MATCHES VOLTAGES ABOVE = " + realTimeVolts[vertClicked]);
                print("GET VALUE AT INDEX CLICKED = " + realTimeVolts.GetValue(vertClicked));
            }
        }
        return vertClicked;
    }

    #region Getter Functions
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


