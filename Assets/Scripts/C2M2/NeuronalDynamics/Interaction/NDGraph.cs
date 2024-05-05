using C2M2.Interaction;
using C2M2.NeuronalDynamics.Interaction.UI;
using UnityEngine;

[RequireComponent(typeof(GrabRescaler))]
[RequireComponent(typeof(NDLineGraph))]
[RequireComponent (typeof(AudioSource))]
[RequireComponent(typeof(ONSPAudioSource))]
public class NDGraph : NDInteractables
{
    public NDGraphManager GraphManager { get { return simulation.graphManager; } }

    private GrabRescaler grabRescaler;
    public NDLineGraph ndlinegraph;

    
    void Awake()
    {
        grabRescaler = GetComponent<GrabRescaler>();
        ndlinegraph = GetComponent<NDLineGraph>();
        var audio = this.gameObject.AddComponent<VertexAudio>();
    }

    
    void Update()
    {
        if (simulation == null || GraphManager == null)
        {
            ndlinegraph.DestroyPlot();
        }
    }

    private void OnDestroy()
    {
        GraphManager.graphs.Remove(this);
    }

    public override void Place(int index)
    {
        if (FocusVert == -1)
        {
            Debug.LogError("Invalid vertex given to NDLineGraph");
            Destroy(this);
        }
        name = "Graph(" + simulation.name + ")[vert" + FocusVert + "]";

        GraphManager.graphs.Add(this);
    }

    protected override void AddHitEventListeners()
    {
        HitEvent.OnHover.AddListener((hit) => grabRescaler.Rescale());
    }
}
