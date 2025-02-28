using System;
using System.Collections.Generic;
using Vector = MathNet.Numerics.LinearAlgebra.Vector<double>;
namespace C2M2.NeuronalDynamics.Simulation
{

    public class GatingVariable
    {
        // Name of the gating variable (i.e: n, m, h)
        public string Name { get; }

        // Alpha and Beta functions for the gating variable
        public Func<Vector, Vector> Alpha { get; }
        public Func<Vector, Vector> Beta { get; }

        public double Exponent { get; }

        // Probability is the initial state probability for the gating variable
        public double Probability { get; set; }
        public Vector CurrentState { get; set; }
        public Vector PreviousState { get; set; }

        public GatingVariable(string name, Func<Vector, Vector> alpha, Func<Vector, Vector> beta, double exponent, double probability, int nodeCount)
        {
            Name = name;
            Alpha = alpha;
            Beta = beta;
            Exponent = exponent;
            Probability = probability;
            CurrentState = Vector.Build.Dense(nodeCount, probability);
            PreviousState = CurrentState.Clone();
        }

        public double GetSteadyState(double V)
        {
            Vector voltage = Vector.Build.Dense(1, V);
            double a = Alpha(voltage)[0];
            double b = Beta(voltage)[0];
            if (a + b == 0)
            {
                return 0;
            }
            return a / (a + b);
        }
    }

    public class IonChannel
    {
        public string Name { get; set; }                  // Name of the ion channel (e.g., K+ channel)
        public double Conductance { get; set; }           // g (conductance)
        public double ReversalPotential { get; set; }     // E (reversal potential)
        public List<GatingVariable> GatingVariables { get; set; } // List of gating variables (can be dynamic)
        public bool IsActive { get; set; }                // Indicates if the ion channel is active

        public IonChannel(string name, double conductance, double reversalPotential)
        {
            Name = name;
            Conductance = conductance;
            ReversalPotential = reversalPotential;
            GatingVariables = new List<GatingVariable>();
            IsActive = true;
        }
        public void AddGatingVariable(GatingVariable gatingVariable)
        {
            GatingVariables.Add(gatingVariable);
        }

        public void RemoveGatingVariable(GatingVariable gatingVariable)
        {
            GatingVariables.Remove(gatingVariable);
        }

        public void Activate()
        {
            IsActive = true;
        }

        public void Deactivate()
        {
            IsActive = false;
        }
    }
} 
