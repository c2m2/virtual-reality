using C2M2.Interaction;
using C2M2.Interaction.VR;
using C2M2.NeuronalDynamics.Interaction;
using C2M2.NeuronalDynamics.Interaction.UI;
using UnityEngine;

[RequireComponent(typeof(GrabRescaler))]
[RequireComponent(typeof(NDLineGraph))]
public class NDGraph : NDInteractables
{
    public NDGraphManager GraphManager { get { return simulation.graphManager; } }

    private GrabRescaler grabRescaler;
    public NDLineGraph ndlinegraph;

    //Jaden: Each Plot instance is equipped w/ an AudioSource Component.
    public new AudioSource audio;

    void Awake()
    {
        grabRescaler = GetComponent<GrabRescaler>();
        ndlinegraph = GetComponent<NDLineGraph>();
        var aud = this.gameObject.AddComponent<VertexAudio>();

    }

    // Update is called once per frame
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
