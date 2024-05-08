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
    THIS SCRIPT = Audio is Dynamic/Spatialized. Audible Audio Artifacts when outside audible range from Audio Source. Want to use "time array" to produce audio most likely

    
    Audio is Spatialized or Dynamic or both
    Spatialization :
        Provided by AudioRolloffMode.CustomRolloff
        The NDLineGraph prefab Custom Rolloff field mimics logarithmic rolloff then linear rolloff. Altered by me, the user.
        The X-Axis ont eh graph that displays the curve represents distance from the audio source, and seems to be scaled according to the size of the room by default
    
    (1) Run the coroutine in Start() ---> Spatialized Audio
        Plots play smooth audio based on ONLY initial voltage value of the neuron
        meaning if the neuron is blue, and you place a plot, no matter what the plot will produce the same frequency
        Downsides: Audioble pops in audio persist, even when far away from audio source.

    (2) Run the Coroutine in Update() --> Dynamic Audio
        Plots play dynamic audio. I Verified this by raycasting on the neuron or placing clamps
        Downsides: Audible "pops" in the audio persist, even when far away from the audio source
        Downsides: Audio played is audibly dynamic, but not smooth Sin Wave like (1)
      
 */

[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(ONSPAudioSource))]
public class VertexAudio : MonoBehaviour
{
    SparseSolverTestv1 sparsecomponent;
    NDGraph audioassociatedplot;
    private int samplerate = 48100;//universal standard samplerate
    AudioSource audioref;

    private float frequencyMin = 600f;
    private float frequencyMax = 1200f;
    private double VoltageMin = -0.02;
    private double VoltageMax = 0.05;
    private double frequency, voltageCapped;
    private double phase = 0;

    //private static int count_times_audio_clip_created = 0;

    /* Each plot instance holds a copy of this script that allows the plot to produce audio */
    private void Awake()
    {
        audioassociatedplot = GetComponent<NDGraph>();
    }

    private void Start()
    {
        audioref = GetComponent<AudioSource>();
        var vraudio = this.gameObject.AddComponent<ONSPAudioSource>();
        StartCoroutine(DynamicAudioVolts(audioassociatedplot, sparsecomponent));
    }

    private void Update()
    {
        //StartCoroutine(DynamicAudioVolts(audioassociatedplot, sparsecomponent));
    }

    public void SetONSPAudioProperties(ONSPAudioSource source)
    {
        source.VolumetricRadius = 0;//For "Point" Audio Sources, the value 0 is recommended. Stated in documentation for the Oculus VR Plugin
        source.Gain = 1f;
        source.EnableSpatialization = true;
        source.UseInvSqr = true;
    }
    private void SetAudioSourceProperties(AudioSource source)
    {
        source.loop = false;
        source.volume = 1f;
        source.rolloffMode = AudioRolloffMode.Custom;// NDLineGraph PREFAB showcases the rolloff graph
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
        double[] waveform = neuron.Get1DValues();
        int vertClicked = plotFocusVert(plot);

        /*
        Map incoming voltage from clicked vertex to a frequency : Minimum Freq <= frequency <= Max Frequency
          
        (1)Does "index" equate to each index of time array?
        I TRIED the code below, if (1) happened to be true.  
            phase = 2 * Mathf.PI * frequency * index;
            -The output was dynamic but overshadowed by weird audio in the background. So I deleted the code.

         (2) Why does this code below produce dynamic audio? Is this ideal? I need to trace this on paper or whiteboard.
         
         */
        int index = 0;
        float[] clickedvert = new float[samplerate];
        while (index < samplerate)
        {
            voltageCapped = Mathf.Min(Mathf.Max((float)waveform[vertClicked], (float)VoltageMin), (float)VoltageMax);
            frequency = frequencyMin + (frequencyMax - frequencyMin) * ((voltageCapped - VoltageMin) / (VoltageMax - VoltageMin));
            phase += 2 * Mathf.PI * (frequency / samplerate);
            clickedvert[index] = Mathf.Sin((float)phase);
            index++;
        }

        /*Set mapped frequencies as clip sample data. Sample data ranges from -1 to 1.*/
        AudioClip clip = AudioClip.Create("DynamicAudio", (int)(samplerate), 1, (int)samplerate, false);
        clip.SetData(clickedvert, 0);
        SetAudioSourceProperties(src);
        SetONSPAudioProperties(vrsource);
        src.clip = clip;
        /* For testing how many clips created. When Coroutine is inside Start() this creates 2 clips for some reason
        count_times_audio_clip_created++;
        print("Clips created = " +  count_times_audio_clip_created);
        */
        while (src.isActiveAndEnabled)
        {
            src.Play();
            yield return new WaitForSeconds(clip.length);
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
            if (condensedVertPos == plotWorldSpacePos(plot))
            {
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
            if (condensedVertPos == clampWorldSpacePos(clamp))
            {
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
