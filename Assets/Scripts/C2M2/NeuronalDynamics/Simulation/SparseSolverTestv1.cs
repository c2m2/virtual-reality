using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;
using Vector = MathNet.Numerics.LinearAlgebra.Vector<double>;
using CSparse.Storage;
using CSparse.Double.Factorization;
using CSparse;
using C2M2.Utils;
using C2M2.NeuronalDynamics.UGX;
namespace C2M2.NeuronalDynamics.Simulation
{
    /// <summary>
    /// This is the sparse solver class for solving the Hodgkin-Huxley equations for the propagation of action potentials. Below are the HH equations
    /// this is a system of partial differential equations (PDE), the first equation is for the membrane potential (voltage) it is time spatially dependent
    /// and the remaining equations are the ODE equations for the state variables n,m,h (these are unitless). The system is non-trivial in that the PDE equation on Voltage
    /// is coupled to the ODE equations using n,m,h and these ODE equations are also non-linear.
    /// 
    ///\f[\frac{a}{2R}\frac{\partial^2V}{\partial x^2}=C\frac{\partial V}{\partial t}+\bar{g}_{K}n^4(V-V_k)+\bar{g}_{Na}m^3h(V-V_{Na})+\bar{g}_l(V-V_l)\f]
    ///
    ///\f[\frac{dn}{dt}=\alpha_n(V)(1-n)-\beta_n(V)n\f]
    ///
    ///\f[\frac{dm}{dt}=\alpha_m(V)(1-m)-\beta_m(V)m\f]
    ///
    ///\f[\frac{dh}{dt}=\alpha_h(V)(1-h)-\beta_h(V)h\f]
    ///
    /// The main solver begins with the function call <c>Solve()</c> prior to each iteration of <c>Solve()</c> the new voltage
    /// values <c>U</c> are sent to this class and then new <c>U</c> are sent out from <c>Solve()</c>
    /// 
    /// The solver currently uses SBDF2 (semi-implicit) Backward Difference 2
    /// The solver takes into account the non-uniform radii of the geometry and the non-uniform edgelength of the geometry
    /// 
    /// These parameters need to be made available to the user to modify for their particular simulation parameters
    /// The rate functions are defined at the end
    /// Note: Very important --> ALL UNITS FOR THE SOLVER ARE IN MKS, therefore when modifying the color bars ranges, and raycast/clamp hit values
    /// they need to be in [V] not [mV] so if you intend to use 50 [mV] it needs to be coded as 0.05 [V]
    /// 
    /// The simulation parameters are defined first, initial voltage hit value for raycasting, the endTime, time step size (k)
    /// Also other options are defined here such as having the SomaOn for clamping the soma on for voltage clamp tests (this is 
    /// mostly used for verifying the voltage output against Yale Neuron).
    /// </summary>

    //tex: Below are the Hodgkin Huxley equations
    //$$\frac{a}{2R}\frac{\partial^2V}{\partial x^2}=C\frac{\partial V}{\partial t}+\bar{g}_{K}n^4(V-V_k)+\bar{g}_{Na}m^3h(V-V_{Na})+\bar{g}_l(V-V_l)$$
    //$$\frac{dn}{dt}=\alpha_n(V)(1-n)-\beta_n(V)n$$
    //$$\frac{dm}{dt}=\alpha_m(V)(1-m)-\beta_m(V)m$$
    //$$\frac{dh}{dt}=\alpha_h(V)(1-h)-\beta_h(V)h$$

    public class SparseSolverTestv1 : NDSimulation
    {
        ///<summary>
        /// This is the voltage for the voltage clamp, this is primarily used for when we do the convergence analysis of the code using a 
        /// soma clamp at 50 [mV], the units for voltage in the solver is [V] that is why <c>vstart</c> is set to 0.05
        ///</summary>
        public double vstart = 0.050;         
        ///<summary>
        /// [ohm.m] resistance.length, this is the axial resistence of the neuron, increasing this value has the effect of making the AP waves more localized and slower conduction speed
        /// decreasing this value has the effect of make the AP waves larger and have a faster conduction speed
        /// </summary>
        private double res = 300.0 * 1.0E-2;
        /// <summary>
        /// [F/m2] capacitance per unit area, this is the plasma membrane capacitance, this a standard value for the capacitance
        /// </summary>
        private double cap = 1.0 * 1.0E-2;
        /// <summary>
        /// [S/m2] potassium conductance per unit area, this is the Potassium conductance per unit area, it is used in this term
        /// \f[\bar{g}_{K}n^4(V-V_k)\f]
        /// where \f$n\f$ is the state variable, and \f$V_k\f$ is the reversal potential.
        /// </summary>
        private double gk = 5.0 * 1.0E1;
        /// <summary>
        /// [S/m2] sodium conductance per unit area, this is the Sodium conductance per unit area, it is used in this term
        /// \f[\bar{g}_{Na}m^3h(V-V_{Na})\f]
        /// where \f$m,h\f$ are the state variables, and \f$V_{Na}\f$ is the reversal potential for sodium.
        /// </summary>
        private double gna = 50.0 * 1.0E1;
        /// <summary>
        /// [S/m2] leak conductance per unit area, this is the leak conductance per unit area, it is used in this term
        /// \f[\bar{g}_{l}(V-V_l)\f]
        /// \f$V_l\f$ is the leak reversal potential.
        /// </summary>
        private double gl = 0.0 * 1.0E1;
        /// <summary>
        /// [V] potassium reversal potential
        /// </summary>
        private double ek = -90.0 * 1.0E-3;
        /// <summary>
        /// [V] sodium reversal potential
        /// </summary>
        private double ena = 50.0 * 1.0E-3;
        /// <summary>
        /// [V] leak reversal potential
        /// </summary>
        private double el = -70.0 * 1.0E-3;
        /// <summary>
        /// [] potassium channel state probability, unitless
        /// </summary>
        private double ni = 0.0376969;
        /// <summary>
        /// [] sodium channel state probability, unitless
        /// </summary>
        private double mi = 0.0147567;
        /// <summary>
        /// [] sodium channel state probability, unitless  
        /// </summary>
        private double hi = 0.9959410;        
        // Calcium Conductance and Reversal Potential
        private double gca = 1.0 * 1.0E1;
        private double eca = 270.0 * 1.0E-3;
        private double qx = 2;
        private double rx = 1;
        // These initial probability states need to be verified
        private double qi = 0.2;
        private double ri = 0.8;
        /// </summary>
        /// [n] potassium channel state exponent, unitless
        /// </summary>
        private double nx = 4;
        /// </summary>
        /// [m] sodoium channel state exponent, unitless
        /// </summary>
        private double mx = 3;
        /// </summary>
        /// [h] sodium channel state exponent, unitless
        /// </summary>
        private double hx = 1;
        /// <summary>
        /// These are the solution vectors for the voltage <code>U</code>
        /// the state <c>M</c>, state <c>N</c>, and state <c>H</c>
        /// </summary>
        private Vector U;
        /// <summary>
        /// This is the U that gets modified during the step before U is set to it.
        /// </summary>
        private Vector U_Active;
        /// <summary>
        /// These are the solution vectors for the voltage <code>U</code>
        /// the state <c>M</c>, state <c>N</c>, and state <c>H</c>
        /// </summary>
        private Vector M, N, H; //Replaced with currentStates[] array
        private Dictionary<string, Vector> currentStates;
        /// <summary>
        /// This is for the synaptic current, it is not the current but the SBDF2 explicit component for the additional current term
        /// </summary>
        private Vector Isyn;
        /// <summary>
        /// this is for storing previous states
        /// </summary>
        private Vector Upre, Npre, Mpre, Hpre; // Replace with previousStates[] array
        private Dictionary<string, Vector> previousStates;
        /// <summary>
        /// This is a vector the Reaction terms
        /// </summary>
        private Vector R;       
        /// <summary>
        /// This is an array for the right hand side of the problem Ax = b
        /// </summary>
        private double[] b;            
        /// <summary>
        /// Declaration for the list of IonChannels
        /// </summary>
        private List<IonChannel> ionChannels;
        /// <summary>
        /// Declaration for the list of active IonChannels
        /// </summary>
        private List<IonChannel> activeIonChannels;
        /// <summary>
        /// Temporary state vector
        /// </summary>
        private Vector tempState;

        List<double> reactConst;                            //This is for passing the reaction function constants
        List<CoordinateStorage<double>> sparse_stencils;
        CompressedColumnStorage<double> r_csc;              //This is for the rhs sparse matrix
        CompressedColumnStorage<double> l_csc;              //This is for the lhs sparse matrix
        private SparseLU lu;  

        /// <summary>
        /// Send simulation 1D values, this send the current voltage after the solve runs 1 iteration
        /// it passes <c>curVals</c>
        /// </summary>
        /// <returns>curVals</returns>
        public override double[] Get1DValues()
        {
            /// this initialize the curVals which will be sent back to the VR simulation
            double[] curVals = null;
            /// check if this beginning of the simulation
            if (curentTimeStep > -1)
            {
                Vector curTimeSlice;
                lock (visualizationValuesLock)
                {
                    /// define the current time slice to send and initialize it to the correct size which is the number of vertices in the geometry
                    /// initialize it to the current state of the voltage, this is the voltage we are sending back to vr simulation
                    curTimeSlice = U.SubVector(0, Neuron.nodes.Count);
                }
                //curTimeSlice.Multiply(1, curTimeSlice);

                curVals = curTimeSlice.ToArray();
            }
            return curVals;
        }

        /// <summary>
        /// Receive new simulation 1D index/value pairings
        /// Carefully, notice that <c>val</c> needs to be multiplied by 0.001 this is because
        /// the hit value is in [mV] and the solver uses [V]
        /// </summary>
        /// <param name="newValues"></param>
        public override void Set1DValues((int, double)[] newValues)
        {
            foreach ((int, double) newVal in newValues)
            {
                if (newVal.Item1 >= 0 && newVal.Item1 < Neuron.nodes.Count)
                {
                    // perform a rank1 update solve to properly update with added dirichelet boundary conditions
                    // from a raycast, or voltage clamp. This is done because with a voltage clamp you are imposing
                    // a dirichelet B.C. which requires solving an updated diffusion problem with identity rows.
                    U_Active = Vector.Build.DenseOfVector(DircheletRank1UpdateSolve(newVal));
                }
            }
        }

        /// <summary>
        /// this perform a Rank1UpdateSolve to properly adjust for added dirichelet boundary conditions.
        /// </summary>
        /// <param name="newVal"></param>
        /// <returns></returns>
        public Vector DircheletRank1UpdateSolve((int, double) newVal)
        {
            double[] bj = new double[Neuron.nodes.Count];
            double[] z = new double[Neuron.nodes.Count];
            double[] y = new double[Neuron.nodes.Count];
                        
            Vector ej = Vector.Build.Dense(Neuron.nodes.Count, 0.0);
            Vector rj = ej.Clone();
            Vector ZZ = ej.Clone();
            Vector YY = ej.Clone();

            R.At(newVal.Item1, newVal.Item2);
            ej = Vector.Build.Dense(Neuron.nodes.Count, 0.0);
            ej.At(newVal.Item1, 1.0);
            (l_csc.Transpose()).Multiply(ej.ToArray(), bj);
            rj = Vector.Build.DenseOfArray(bj);
            rj.At(newVal.Item1, rj[newVal.Item1] - 1);

            lu.Solve(ej.ToArray(), z);
            lu.Solve(R.ToArray(), y);
            
            ZZ = Vector.Build.DenseOfArray(z);
            YY = Vector.Build.DenseOfArray(y);

            return YY.Add(ZZ.Multiply(rj.DotProduct(YY) / (1 - rj.DotProduct(ZZ))));
        }

        /// <summary>
        /// Receives 1D information for synaptic communication
        /// newValues = is a list of (presynapse, postsynapse)
        /// </summary>
        /// <param name="newValues"></param>
        internal override void SetSynapseCurrent(List<(Synapse,Synapse)> newValues)
        {
            // iterate through teach (pre,post) synapse pair
            foreach ((Synapse,Synapse) newVal in newValues)
            {
                if ((newVal.Item1 != null) && (newVal.Item2 != null))
                {
                    if (newVal.Item1.FocusVert >= 0 && newVal.Item1.FocusVert < Neuron.nodes.Count && newVal.Item2.FocusVert >= 0 && newVal.Item2.FocusVert < Neuron.nodes.Count)
                    {
                        // compute the synaptic current at the postsynapse using an explicity SBDF update
                        Isyn[newVal.Item2.FocusVert] += SynapseExplicitSBDF(newVal);
                    }
                }
            }
        }
                
        /// <summary>
        /// This computes the explicit update for the Isynaptic current
        /// the input is a tuple (presyn, postsyn) = (item1, item2) respectively
        /// each synapse contains information
        /// item1.nodeindex = index on the 1d geometry
        /// item1.voltage = voltage at that node
        /// </summary>
        /// <param name="newVal"></param>
        /// <returns></returns>
        public double SynapseExplicitSBDF((Synapse, Synapse) newVal)
        {
            double area = new double();
            List<double> Icurrs = new List<double>();

            // compute surface area at postsynaptic location
            area = 2 * System.Math.PI * Neuron.nodes[newVal.Item2.FocusVert].NodeRadius * Neuron.TargetEdgeLength * 1e-12;

            //Icurrs[0] is current synaptic state, and Icurrs[1] is previous synaptic state
            Icurrs = SynapseCurrentFunction(newVal, newVal.Item1.currentModel);

            // If the user should use unrealistic biological parameters, this will check the current and set the current appropriately if the current goes beyond
            // biologically accurate currents
            if (Double.IsNaN(Icurrs[0]) || Double.IsNaN(Icurrs[1]) || (Icurrs[0] > 0.5e-9) || (Icurrs[1]>0.5e-9))
            {   
                Icurrs[0] = 1.0e-16; Icurrs[1] = 0.9e-16;
            }

            // this is the SBDF calculation using the Icurr of the current state, and Icurr of the previous state
            return (2.0 / 3.0) * timeStep / (cap * area) * (2.0 * Icurrs[0] - Icurrs[1]);
        }

        /// <summary>
        /// This is the synaptic current function
        /// the input is a tuple (presyn, postsyn) = (item1, item2) respectively
        /// each synapse contains information
        /// item1.nodeindex = index on the 1d geometry
        /// item1.voltage = voltage at that node
        /// </summary>
        /// <param name="newVal"></param>
        /// <returns></returns>
        public List<double> SynapseCurrentFunction((Synapse, Synapse) newVal, Synapse.Model model)
        {
            // allocate a small for the two currents, one for current state, and one for previous state
            List<double> Icurrs = new List<double>();

            // get the pre and post synaptic voltages
            double presynVoltage = newVal.Item1.simulation.Get1DValues()[newVal.Item1.FocusVert];
            double presynVoltage0 = Upre[newVal.Item1.FocusVert];
            double voltageThreshold;
            if (model == Synapse.Model.NMDA)
            {
                voltageThreshold = 0.038;
            }
            else
            {
                voltageThreshold = -0.05;
            }

            if ((presynVoltage >= voltageThreshold) && (presynVoltage0< voltageThreshold))
            { newVal.Item1.ActivationTime = GetSimulationTime(); }
                                   
            // if the presynapse is below a threshold, then the synapse is INACTIVE
            if (presynVoltage <= voltageThreshold)
            {
                Icurrs = new List<double>
                {
                    0.0,    // zero current at postsynapse while INACTIVE
                    0.0    // zero current at postsynapse while INACTIVE
                };
            }
            else // if the presynaptic voltage is above threshold, then do not update activation time and compute the new current
            {
                if (GetSimulationTime() > (newVal.Item1.ActivationTime + 3.0e-3))
                { newVal.Item1.ActivationTime = GetSimulationTime(); }

                Icurrs = new List<double>();
                if (model == Synapse.Model.NMDA)
                {
                    Icurrs.Add(NMDAFunction(U_Active[newVal.Item2.FocusVert], GetSimulationTime(), newVal.Item1.ActivationTime));         // compute current synaptic state using current voltage state
                    Icurrs.Add(NMDAFunction(Upre[newVal.Item2.FocusVert], GetSimulationTime(), newVal.Item1.ActivationTime));             // compute previous synaptic state using previous voltage state
                }
                else
                {
                    Icurrs.Add(GABAFunction(U_Active[newVal.Item2.FocusVert], GetSimulationTime(), newVal.Item1.ActivationTime));         // compute current synaptic state using current voltage state
                    Icurrs.Add(GABAFunction(Upre[newVal.Item2.FocusVert], GetSimulationTime(), newVal.Item1.ActivationTime));             // compute previous synaptic state using previous voltage state
                }
                
                };
            
            return Icurrs;
        }

        /// <summary>
        /// This is the NMDA Synapse function borrowed from Rothman, Jason S. "Modeling Synapses." (2014).
        /// </summary>
        /// <param name="v"></param> this is the postsynaptic voltage
        /// <param name="t"></param> this is the current simulation time
        /// <param name="ts"></param> this is the activation time of the synapse, this is NOT the time the synapse is placed
        /// <returns></returns>
        public double NMDAFunction(double v, double t, double ts)
        {            
            double Erev = -0.0125;              // reversal potential for synapse
            double taud = 3.0e-4;               // decay constant from function
            double g = 25e-9;              // borrowed from Rothman Paper they mention 10's of nanosiemens
                        
            return g * (1.0 / (1.0 + System.Math.Exp(-1.0 * (v + 0.0128) / 0.0224))) * System.Math.Exp(-1.0 * (t - ts) / taud) * (v - Erev);           
        }

        /// <summary>
        /// This is the GABA Synapse function borrowed from Rothman, Jason S. "Modeling Synapses." (2014).
        /// </summary>
        /// <param name="v"></param> this is the postsynaptic voltage
        /// <param name="t"></param> this is the current simulation time
        /// <param name="ts"></param> this is the activation time of the synapse, this is NOT the time the synapse is placed
        /// <returns></returns>
        public double GABAFunction(double v, double t, double ts)
        {
            double Erev = -0.065;              // reversal potential for synapse
            double taud = 3.0e-4;               // decay constant from function
            double g = 30e-12;              // borrowed from Rothman Paper this is conductance of GABA receptor

            return g * System.Math.Exp(-1.0 * (t - ts) / taud) * (v - Erev);
        }

        /// <summary>
        /// This is a small routine call to initialize the Neuron Cell
        /// this will initialize the solution vectors which are <c>U</c>, <c>M</c>, <c>N</c>, and <c>H</c>
        /// </summary>
        protected override void PreSolve()
        {
            GameManager g = GameManager.instance;
            // if loading, the values from file will be set in BuildVectors and Set1DValues
            if (!g.Loading) 
            {
                InitializeNeuronCell();
            }
            else BuildVectors(g.U, g.M, g.N, g.H, g.Upre, g.Mpre, g.Npre, g.Hpre);

            ///<c>R</c> this is the reaction vector for the reaction solve
            R = Vector.Build.Dense(Neuron.nodes.Count);            
            
            tempState = Vector.Build.Dense(Neuron.nodes.Count, 0);
            ///<c>reactConst</c> this is a small list for collecting the conductances and reversal potential which is sent to the reaction solve routine
            reactConst = new List<double> { gk, gna, gl, ek, ena, el };

            /// this sets the target time step size
            // timeStep = SetTargetTimeStep(cap, 2 * Neuron.MaxRadius, 2 * Neuron.MinRadius, Neuron.TargetEdgeLength, ionChannels, res, 1.0);
            timeStep = SetTargetTimeStep(cap, 2 * Neuron.MaxRadius,2*Neuron.MinRadius, Neuron.TargetEdgeLength, gl, res, 1.0);
            // UnityEngine.Debug.Log("Target Time Step = " + timeStep);

            ///<c>List<CoordinateStorage<double>> sparse_stencils = makeSparseStencils(Neuron, res, cap, k);</c> Construct sparse RHS and LHS in coordinate storage format, no zeros are stored \n
            /// <c>sparse_stencils</c> this is a list which contains only two matrices the LHS and RHS matrices for the Crank-Nicolson solve
            sparse_stencils = makeSparseStencils(Neuron, res, cap, timeStep);
            ///<c>CompressedColumnStorage</c> call Compresses the sparse matrices which are stored in <c>sparse_stencils[0]</c> and <c>sparse_stencils[1]</c>
            r_csc = CompressedColumnStorage<double>.OfIndexed(sparse_stencils[0]); //null;
            l_csc = CompressedColumnStorage<double>.OfIndexed(sparse_stencils[1]); //null;
            ///<c>double [] b</c> we define storage for the diffusion solve part
            b = new double[Neuron.nodes.Count];
            ///<c>var lu = SparseLU.Create(l_csc, ColumnOrdering.MinimumDegreeAtA, 0.1);</c> this creates the LU decomposition of the HINES matrix which is defined by <c>l_csc</c>
            lu = SparseLU.Create(l_csc, ColumnOrdering.MinimumDegreeAtA, 0.1);
        }

        /// <summary>
        /// This is the main solver, it is running on it own thread.
        /// The solver using SBDF2 for time steping, the implicit part is used for the diffusion and the explicit
        /// is for the reaction terms and state variables
        /// </summary>     
        protected override void SolveStep(int t)
        {            
            U_Active.Multiply(4.0 / 3.0, R);
            R.Add(reactF(activeIonChannels, U_Active, currentStates, cap).Multiply((4.0 / 3.0) * timeStep), R);
            R.Add(Upre.Multiply(-1.0 / 3.0), R);
            R.Add(reactF(activeIonChannels, Upre, previousStates, cap).Multiply((-2.0 / 3.0) * timeStep), R);
            // R.Add(reactF(reactConst, U_Active, N, M, H, cap).Multiply((4.0 / 3.0) * timeStep), R);
            // R.Add(Upre.Multiply(-1.0 / 3.0), R);
            // R.Add(reactF(reactConst, Upre, Npre, Mpre, Hpre, cap).Multiply((-2.0 / 3.0) * timeStep), R);
            R.Add(Isyn, R);
            Isyn.Multiply(0.0, Isyn); // reset synaptic source this ensures that when you remove the synapse that Isyn becomes 0; therefore, current is not being sent to postsynapse once synapse is removed

            lu.Solve(R.ToArray(), b);

            // tempState = N.Clone();
            // stateexplicitSBDF2(N, Npre, fS(N, an(U_Active), bn(U_Active)), fS(Npre, an(Upre), bn(Upre)), timeStep);
            // Npre = tempState.Clone();

            // tempState = M.Clone();
            // stateexplicitSBDF2(M, Mpre, fS(M, am(U_Active), bm(U_Active)), fS(Mpre, am(Upre), bm(Upre)), timeStep);
            // Mpre = tempState.Clone();

            // tempState = H.Clone();
            // stateexplicitSBDF2(H, Hpre, fS(H, ah(U_Active), bh(U_Active)), fS(Hpre, ah(Upre), bh(Upre)), timeStep);
            // Hpre = tempState.Clone();

            foreach (var channel in activeIonChannels)
            {
                foreach (var gatingVariable in channel.GatingVariables)
                {
                    tempState = currentStates[gatingVariable.Name].Clone();
                    // Use stateexplicitSBDF2 to update the gating variable
                    stateexplicitSBDF2(
                        currentStates[gatingVariable.Name],
                        previousStates[gatingVariable.Name],
                        fS(currentStates[gatingVariable.Name], gatingVariable.Alpha(U_Active), gatingVariable.Beta(U_Active)),
                        fS(previousStates[gatingVariable.Name], gatingVariable.Alpha(Upre), gatingVariable.Beta(Upre)), 
                        timeStep
                    );

                    // Update previous state for the next time step
                    previousStates[gatingVariable.Name] = tempState.Clone();
                }
            }

            Upre = U_Active.Clone();
            U_Active.SetSubVector(0, Neuron.nodes.Count, Vector.Build.DenseOfArray(b));
                       
        }

        internal override void SetOutputValues()
        { lock (visualizationValuesLock) U = U_Active.Clone(); }
		
        /// <summary>
        /// This function sets the target time step size, below is the formula for the conduction speed of the action potential (wave speed)
        ///
        /// \f[v = \frac{1}{C}\sqrt{\frac{d}{R_a R_{mem}}}]
        /// where
        /// \f[R_{mem} = \frac{1}{g_{Na}m^3 h + g_K n^4}]
        /// 
        /// and n,m,h are the probability states which vary in time and depend on the voltage
        /// Notice that
        /// 
        /// \f[\frac{1}{R_{mem}}\leq g_{Na}+g_K=G_{mem-theor-max}]
        /// 
        /// therefore we define the max conduction speed vmax as
        /// 
        /// v_{max} = \frac{1}{C}\sqrt{\frac{d_max\cdot (g_{Na}+g_K)}{R_a}}
        /// 
        /// then we solve for our target \f[Delta t] by computing
        /// 
        /// \f[\Delta t = \frac{\Delta x}{v_{max}}]
        /// 
        /// where \f[Delta x] is the median edge length, in this case we use the average this is because our geometries are regularize and there
        /// are not excessively small edges in the graph geometry.
        ///  
        /// </summary>
        /// <param name="cap"></param> this is the capacitance
        /// <param name="maxDiameter"></param> this is the maximum diameter
        /// <param name="edgeLength"></param> this is the target edge length of the graph geometry
        /// <param name="gna"></param> this is the sodium conductance
        /// <param name="gk"></param> this is the potassium conductance
        /// <param name="res"></param> this is the axial resistance
        /// <param name="Rmemscf"></param> this is membrane resistance scale factor, since this is only a fraction of theoretical maximum
        /// <returns></returns>
        //     public static double SetTargetTimeStep(double cap, double maxDiameter, double minDiameter, double edgeLength , List<IonChannel> activeIonChannels, double res, double cfl)
        // {
        //     /// here we set the minimum time step size and maximum time step size
        //     /// the dtmin is based on prior numerical experiments that revealed that for each refinement level the 
        //     /// voltage profiles were visually accurate when compared to Yale Neuron for delta t at least 2 microseconds
        //     /// we want to avoid using dtmin; therefore I compute the upper bound (and lower bound for reference)
        //     //double dtmin = 2e-6;  
        //     double dtmax = 50e-6;
        //     double dt;

        //     double scf = 1E-6; // to convert to micrometer of edgelengths and radii don't forget this!!!!
        //     double effectiveConductance = 0.0;

        //     foreach (IonChannel channel in activeIonChannels)
        //     {
        //         effectiveConductance += channel.Conductance;
        //     }

        //     if (effectiveConductance == 0.0) effectiveConductance = 1.0; // Avoid division by zero

        //     double upper_bound = cap * edgeLength * scf * System.Math.Sqrt(res / (effectiveConductance * minDiameter * scf));
        //     //double lower_bound = cap * edgeLength*scf * System.Math.Sqrt(res / (gna + gk + gl) * maxDiameter*scf);
        //     //GameManager.instance.DebugLogSafe("upper_bound = " + upper_bound.ToString());

        //     // some cells may have an upper bound that is too large for the solver, so choose the smaller of the two dtmax or upper_bound
        //     dt = System.Math.Min(upper_bound,dtmax);
        //     //GameManager.instance.DebugLogSafe("lower_bound = " + lower_bound.ToString());
        //     return dt;       
        // }
            public static double SetTargetTimeStep(double cap, double maxDiameter, double minDiameter, double edgeLength, double gl, double res, double cfl)
        {
            /// here we set the minimum time step size and maximum time step size
            /// the dtmin is based on prior numerical experiments that revealed that for each refinement level the 
            /// voltage profiles were visually accurate when compared to Yale Neuron for delta t at least 2 microseconds
            /// we want to avoid using dtmin; therefore I compute the upper bound (and lower bound for reference)
            //double dtmin = 2e-6;  
            double dtmax = 50e-6;
            double dt;

            double gll = gl; double scf = 1E-6; // to convert to micrometer of edgelengths and radii don't forget this!!!!

            // what happens if the leak conductance is 0
            if (gll == 0.0) { gll = 1.0; }
            
            double upper_bound = cap * edgeLength*scf * System.Math.Sqrt(res / (gll*minDiameter*scf));
            //double lower_bound = cap * edgeLength*scf * System.Math.Sqrt(res / (gna + gk + gl) * maxDiameter*scf);
            //GameManager.instance.DebugLogSafe("upper_bound = " + upper_bound.ToString());

            // some cells may have an upper bound that is too large for the solver, so choose the smaller of the two dtmax or upper_bound
            dt = System.Math.Min(upper_bound,dtmax);
            //GameManager.instance.DebugLogSafe("lower_bound = " + lower_bound.ToString());
            return dt;       
        }

        public void InitializeIonChannel()
        {
            ionChannels = new List<IonChannel>();
            activeIonChannels = new List<IonChannel>();

            // Alpha and Beta functions
            // For Potassium Channels
            Func<Vector, Vector> alpha_n = voltage => {
                var Vin = voltage.Clone();
                Vin.Multiply(1.0E3, Vin); 
                return (1.0E3) * (0.032) * (15.0 - Vin).PointwiseDivide(((15.0 - Vin) / 5.0).PointwiseExp() - 1.0);
            };

            Func<Vector, Vector> beta_n = voltage => {
                var Vin = voltage.Clone();
                Vin.Multiply(1.0E3, Vin);
                return (1.0E3) * (0.5) * ((10.0 - Vin) / 40.0).PointwiseExp();
            };

            // For Sodium Channels

            Func<Vector, Vector> alpha_m = voltage => {
                var Vin = voltage.Clone();
                Vin.Multiply(1.0E3, Vin);
                return (1.0E3) * (0.32) * (13.0 - Vin).PointwiseDivide(((13.0 - Vin) / 4.0).PointwiseExp() - 1.0);
            };

            Func<Vector, Vector> beta_m = voltage => {
                var Vin = voltage.Clone();
                Vin.Multiply(1.0E3, Vin);
                return (1.0E3) * (0.28) * (Vin - 40.0).PointwiseDivide(((Vin - 40.0) / 5.0).PointwiseExp() - 1.0);
            };

            Func<Vector, Vector> alpha_h = voltage => {
                var Vin = voltage.Clone();
                Vin.Multiply(1.0E3, Vin);
                return (1.0E3) * (0.128) * ((17.0 - Vin) / 18.0).PointwiseExp();
            };

            Func<Vector, Vector> beta_h = voltage => {
                var Vin = voltage.Clone();
                Vin.Multiply(1.0E3, Vin);
                return (1.0E3) * (4.0) / (((40.0 - Vin) / 5.0).PointwiseExp() + 1.0);
            };

            // For Calcium Channels

            Func<Vector, Vector> alpha_q = voltage => {
                var Vin = voltage.Clone();
                Vin.Multiply(1.0E3, Vin);
                return (1.0E3) * (0.055) * (27.0 - Vin) / (((-27 - Vin) / 3.8).PointwiseExp() - 1.0);
            };
            
            Func<Vector, Vector> beta_q = voltage => {
                var Vin = voltage.Clone();
                Vin.Multiply(1.0E3, Vin);
                return (1.0E3) * (0.94) * (((-75.0 - Vin) / 17.0).PointwiseExp());
            };

            Func<Vector, Vector> alpha_r = voltage => {
                var Vin = voltage.Clone();
                Vin.Multiply(1.0E3, Vin);
                return (1.0E3) * (0.000457) * (((-13 - Vin) / 50.0).PointwiseExp());
            };
            
            Func<Vector, Vector> beta_r = voltage => {
                var Vin = voltage.Clone();
                Vin.Multiply(1.0E3, Vin);
                return (1.0E3) * (0.0065) / (((-15.0 - Vin) / 28.0).PointwiseExp() + 1.0);
            };


            // Initialize ion channels and gating variables
            IonChannel potassiumChannel = new IonChannel("Potassium Channel", gk, ek);
            potassiumChannel.AddGatingVariable(new GatingVariable("n", alpha_n, beta_n, nx, ni, Neuron.nodes.Count));

            IonChannel sodiumChannel = new IonChannel("Sodium Channel", gna, ena);
            sodiumChannel.AddGatingVariable(new GatingVariable("m", alpha_m, beta_m, mx, mi, Neuron.nodes.Count));
            sodiumChannel.AddGatingVariable(new GatingVariable("h", alpha_h, beta_h, hx, hi, Neuron.nodes.Count));

            
            IonChannel calciumChannel = new IonChannel("Calcium Channel", gca, eca);
            calciumChannel.AddGatingVariable(new GatingVariable("q", alpha_q, beta_q, qx, qi, Neuron.nodes.Count));
            calciumChannel.AddGatingVariable(new GatingVariable("r", alpha_r, beta_r, rx, ri, Neuron.nodes.Count));

            IonChannel leakageChannel = new IonChannel("Leakage Channel", gl, el);


            AddIonChannel(potassiumChannel);
            AddIonChannel(sodiumChannel);
            AddIonChannel(calciumChannel);
            AddIonChannel(leakageChannel);
        }

        /// <summary>
        /// This function initializes the voltage vector <c>U</c> and the state vectors
        /// <c>M</c>, <c>N</c>, and <c>H</c> \n
        /// The input <c>Neuron.vertCount</c> is the vertex count of the neuron geometry \n
        /// <c>U</c> is initialized to 0 [V] for the entire cell \n
        /// <c>M</c> is initialized to \f$m_i\f$ which is set by <c>mi</c> \n
        /// <c>N</c> is initialized to \f$n_i\f$ which is set by <c>ni</c> \n
        /// <c>H</c> is initialized to \f$h_i\f$ which is set by <c>hi</c>
        /// </summary>
        private void InitializeNeuronCell()
        {
            lock (visualizationValuesLock)
            {
                U = Vector.Build.Dense(Neuron.nodes.Count, 0.0);
                U_Active = U.Clone();
            }
            Upre = U_Active.Clone();
            Isyn = Vector.Build.Dense(Neuron.nodes.Count, 0.0);

            // Initialize ion channels and associated gating variables
            InitializeIonChannel();
            
            currentStates = new Dictionary<string, Vector>();
            previousStates = new Dictionary<string, Vector>();

            M = Vector.Build.Dense(Neuron.nodes.Count, mi);
            N = Vector.Build.Dense(Neuron.nodes.Count, ni);
            H = Vector.Build.Dense(Neuron.nodes.Count, hi);
            Mpre = M.Clone(); Npre = N.Clone(); Hpre = H.Clone();

            foreach (var channel in ionChannels)
            {
                foreach (var gatingVariable in channel.GatingVariables)
                {
                    // Use the probability from the gating variable to initialize current and previous states
                    currentStates[gatingVariable.Name] = Vector.Build.Dense(Neuron.nodes.Count, gatingVariable.Probability);
                    previousStates[gatingVariable.Name] = currentStates[gatingVariable.Name].Clone();
                }
            }
        }
        /// <summary>
        /// This is for constructing the lhs and rhs of system matrix \n
        /// This will construct a HINES matrix (symmetric), it should be tridiagonal with some off
        /// diagonal entries corresponding to a branch location in the neuron graph \n
        /// The entries are defined by the following:
        /// \f[
        /// \left(-\sum_{k\in\mathcal{N}_j}\eta_kV_k^{n+1}\right)+\omega_jV_j^{n+1}=\left(\sum_{k\in\mathcal{N}_j}\eta_kV_k^{n}\right)+\bar{\omega}_jV_j^{n}
        /// \f]
        /// where
        /// \f[\eta_k = \frac{\gamma_{k, j}\Delta t}{ 2}\f]
        /// and
        /// \f[\omega_j = 1+\frac{\theta_j\Delta t}{ 2} = 1 +\frac{\Delta t\sum_{ p\in\mathcal{ N} _j}\gamma_{ p,j} }{ 2}\f]
        /// and \f$\gamma_{ k,j}\f$ is defined as
        /// \f[\gamma_{k, j}:=\frac{ 1}{ C_mR_a a_j\widetilde{\Delta x_j} }\cdot \frac{ 1}{\left(\frac{ 1} { a_{ k} ^2} +\frac{ 1} { a_j ^ 2}\right)\Delta x_{ { k},j} }\f]
        /// </summary>
        /// <param name="myCell"></param> this is the <c>Neuron</c> that contains all the information about the cell geometry
        /// <param name="res"></param> this is the axial resistance
        /// <param name="cap"></param> this is the membrane capacitance
        /// <param name="k"></param> this is the fixed time step size
        /// <returns>LHS,RHS</returns> the function returns the LHS, RHS stencil matrices for the diffusion solve in sparse format, it is compressed in the main solver routine.
        public static List<CoordinateStorage<double>> makeSparseStencils(Neuron myCell, double res, double cap, double k)
        {
            /// send output matrices as a list {rhs, lhs}\n
            /// <c>List<CoordinateStorage<double>> stencils = new List<CoordinateStorage<double>>();</c> initializes empty list storage for the stencil matrices \n
            /// in this case they are of type <c>CoordinateStorage</c> \n
            List<CoordinateStorage<double>> stencils = new List<CoordinateStorage<double>>();

            /// initialize new coordinate storage \n
            /// <c>var rhs = new CoordinateStorage<double>(myCell.vertCount, myCell.vertCount, myCell.vertCount * myCell.vertCount);</c> this initializes our empty coordinate storage matrices <c>rhs</c> and <c>lhs</c>
            var rhs = new CoordinateStorage<double>(myCell.nodes.Count, myCell.nodes.Count, myCell.nodes.Count * myCell.nodes.Count);
            var lhs = new CoordinateStorage<double>(myCell.nodes.Count, myCell.nodes.Count, myCell.nodes.Count * myCell.nodes.Count);

            /// for keeping track of the neighbors of a node \n
            /// <c>List<int> nghbrlist;</c> this is for collecting the neighbor indices of the current node 
            List<int> nghbrlist;
            int nghbrLen;

            double tempEdgeLen, tempRadius, avgEdgeLengths;
            /// <c>double sumRecip = 0;</c> this is for adding the sum of reciprocals which is in our stencil scheme \n
            double sumRecip = 0;
            double scf = 1E-6;  /// 1e-6 scale factor to convert to micrometers for radii and edge length \n

            for (int j = 0; j < myCell.nodes.Count; j++)
            {
                List<double> edgelengths = new List<double>();
                /// <c>nghbrlist = myCell.nodes[j].AdjacencyList.Keys.ToList();</c> this gets the current neighbor list for node j \n
                nghbrlist = myCell.nodes[j].AdjacencyList.Keys.ToList();
                /// <c>nghbrLen = nghbrlist.Count;</c> this is the length of the neighbor list \n
                nghbrLen = nghbrlist.Count;
                sumRecip = 0;
                /// <c>tempRadius = myCell.nodeData[j].nodeRadius*scf;</c> get the current radius at node j \n
                tempRadius = myCell.nodes[j].NodeRadius*scf;

                /// in this loop we collect the edgelengths that go to node j, and we compute the coefficient given in our paper \n
                foreach (int nghbrIds in nghbrlist)
                {
                    /// <c>tempEdgeLen = myCell.nodes[j].AdjacencyList[nghbrIds]*scf;</c> get the edge length at current node j, to node neighbor p, scale to micro meters \n
                    tempEdgeLen = myCell.nodes[j].AdjacencyList[nghbrIds]*scf;
                    /// <c>edgelengths.Add(tempEdgeLen);</c> put the edge length in the list, this list of edges will have length equal to length of neighbor list \n
                    edgelengths.Add(tempEdgeLen);
                    sumRecip = sumRecip + 1 / (tempEdgeLen * tempRadius * ((1 / (myCell.nodes[nghbrIds].NodeRadius*scf* myCell.nodes[nghbrIds].NodeRadius*scf)) + (1 / (tempRadius * tempRadius))));
                }
                /// get the average edge lengths of neighbors \n
                avgEdgeLengths = edgelengths.Average();
                /// set main diagonal entries using <c>rhs.At()</c>
                /// this is BE method, no oscillations but not as accurate!
                rhs.At(j, j, 1.0);
                lhs.At(j, j, 1 + ((2.0/3.0)*k * sumRecip) / (1.0 * res * cap * avgEdgeLengths));

                /// This is for CN method, this will cause oscillations and flickering!
                //rhs.At(j, j, 1 - (k * sumRecip) / (2.0 * res * cap * avgEdgeLengths));
                //lhs.At(j, j, 1 + (k * sumRecip) / (2.0 * res * cap * avgEdgeLengths));
                /// set off diagonal entries by going through the neighbor list, and using <c>rhs.At()</c>
                for (int p = 0; p < nghbrLen; p++)
                {
                    // this is for CN method, notice the factor of 2
                    //rhs.At(j, nghbrlist[p], k / (2 * res * cap * tempRadius* avgEdgeLengths * edgelengths[p] * ((1 / (myCell.nodes[nghbrlist[p]].NodeRadius*scf * myCell.nodes[nghbrlist[p]].NodeRadius*scf)) + (1 / (tempRadius * tempRadius)))));
                    lhs.At(j, nghbrlist[p], -1.0*(2.0/3.0) * k / (1.0 * res * cap * tempRadius * avgEdgeLengths * edgelengths[p] * ((1 / (myCell.nodes[nghbrlist[p]].NodeRadius*scf * myCell.nodes[nghbrlist[p]].NodeRadius*scf)) + (1 / (tempRadius * tempRadius)))));
                }
            }
            //rhs.At(0, 0, 1);
            //lhs.At(0, 0, 1);

            /// <c>stencil.Add()</c> this adds the completed stencil matrices to the output list
            stencils.Add(rhs);
            stencils.Add(lhs);
            return stencils;
        }

        /// <summary>
        /// This is the reaction term of the HH equation which is defined by
        /// \f[ r(V):=-\frac{\bar{g}_{K}}{C}n^4(V-V_k)-\frac{\bar{g}_{Na}}{C}m^3h(V-V_{Na})-\frac{\bar{g}_l}{C}(V-V_l) \f]
        /// </summary>
        /// <param name="reactConst"></param> these are the conductances and reversal potentials defined by <c>List<double> reactConst = new List<double> { gk, gna, gl, ek, ena, el };</c>
        /// <param name="V"></param> this is the voltage vector
        /// <param name="NN"></param> this is the state vector n
        /// <param name="MM"></param> this is the state vector m
        /// <param name="HH"></param> this is the state vector h
        /// <param name="cap"></param> this is the capacitance
        /// <returns></returns>
        // private static Vector reactF(List<double> reactConst, Vector V, Vector NN, Vector MM, Vector HH, double cap)
        // {
        //     /// initialize the output vector and prod vector these will be used to assemble the different parts of the reaction calculation \n
        //     /// <c>Vector output = Vector.Build.Dense(V.Count, 0.0);</c> initializes the output vector of length equation to number of entries in voltage vector, initialized to 0 \n
        //     /// <c>Vector prod = Vector.Build.Dense(V.Count, 0.0);</c> initializes the product vector of length equation to number of entries in voltage vector, initialized to 0 \n
        //     /// <c>double ek, ena, el, gk, gna, gl; </c> these are the conductances and reversal potentials that we need to assign using <c>reactConst</c> parameter that is sent \n
        //     Vector output = Vector.Build.Dense(V.Count, 0.0);
        //     Vector prod = Vector.Build.Dense(V.Count, 0.0);
        //     double ek, ena, el, gk, gna, gl;
        //     /// this sets the constants for the conductances \n
        //     /// <c>gk = reactConst[0]; gna = reactConst[1]; gl = reactConst[2];</c>
        //     gk = reactConst[0]; gna = reactConst[1]; gl = reactConst[2];
        //     /// this sets constants for reversal potentials \n
        //     /// <c>ek = reactConst[3]; ena = reactConst[4]; el = reactConst[5];</c>
        //     ek = reactConst[3]; ena = reactConst[4]; el = reactConst[5];
        //     /// <c>output.Add(prod.Multiply(gk), output);</c> this adds current due to potassium
        //     prod.SetSubVector(0, V.Count, NN.PointwisePower(4.0));
        // Debug.Log("NN prod: " + prod);
        //     prod.SetSubVector(0, V.Count, (V.Subtract(ek)).PointwiseMultiply(prod));
        //     output.Add(prod.Multiply(gk), output);
        //     /// <c>output.Add(prod.Multiply(gna), output);</c> this adds current due to sodium
        //     prod.SetSubVector(0, V.Count, MM.PointwisePower(3.0));
        //     prod.SetSubVector(0, V.Count, HH.PointwiseMultiply(prod));
        //     prod.SetSubVector(0, V.Count, (V.Subtract(ena)).PointwiseMultiply(prod));
        // Debug.Log("MM prod: " + prod);
        // Debug.Log("HH prod: " + prod);
        //     output.Add(prod.Multiply(gna), output);
        //     /// <c>output.Add((V.Subtract(el)).Multiply(gl), output);</c> this adds leak current
        //     output.Add((V.Subtract(el)).Multiply(gl), output);
        //     /// Return the negative of the total 
        //     output.Multiply(-1.0 / cap, output);

        //     Debug.Log("output = " + output);
        //     return output;
        // }

        private static Vector reactF(List<IonChannel> activeIonChannels, Vector V, Dictionary<string, Vector> gatingStates, double cap)
        {
            Vector output = Vector.Build.Dense(V.Count, 0.0);
            
            foreach (var channel in activeIonChannels)
            {

                Vector prod = Vector.Build.Dense(V.Count, 1.0);
                
                // Only apply leak current directly
                if (channel.Name.Contains("Leak"))
                {
                    output.Add((V.Subtract(channel.ReversalPotential)).Multiply(channel.Conductance), output);
                }

                else
                {
                    foreach (var gatingVariable in channel.GatingVariables)
                    {
                        Vector state = gatingStates[gatingVariable.Name];

                        // Calculate contribution for this channel and gating variable
                        prod.SetSubVector(0, V.Count, state.PointwisePower(gatingVariable.Exponent).PointwiseMultiply(prod));
                    }
                    prod.SetSubVector(0, V.Count, (V.Subtract(channel.ReversalPotential)).PointwiseMultiply(prod));
                }

                output.Add(prod.Multiply(channel.Conductance), output);
            }
            output.Multiply(-1.0 / cap, output);
            return output;
        }

        private void stateexplicitSBDF2(Vector S, Vector Spre, Vector F, Vector Fpre, double dt)
        {
            S.Add(F.Multiply(dt), S); S.Multiply(4.0 / 3.0, S);
            S.Add(Spre.Multiply(-1.0 / 3.0), S); S.Add(Fpre.Multiply(-2.0 * dt / 3.0), S);
        }

        /// <summary>
        /// This is the function for the right hand side of the ODE on state S, which is given by:
        /// \f[\frac{dS}{dt}=\alpha_S(V)(1-S)-\beta_S(V)S\f]
        /// </summary>
        /// <param name="a"></param> this is the rate vector
        /// <param name="b"></param> this is the rate vector
        /// <param name="S"></param> this is the current vector of state S for the geometry
        /// <returns>f(V,N)</returns> the function returns the right hand side of the state N ODE.
        private static Vector fS(Vector S, Vector a, Vector b) { return a.PointwiseMultiply(1 - S) - b.PointwiseMultiply(S); }
       
        /// <summary>
        /// This is \f$\alpha_n\f$ rate function, the rate functions take the form of
        /// \f[
        /// \frac{a_p(V-B_p)}{\exp(\frac{V-B_p}{C_p})-D_p}
        /// \f]
        /// the constants \f$a_p,B_p,C_p,D_p\f$ are manually coded in for this version of the simulation\n
        /// TODO: come up with an implementation where the user can enter in their own parameters (Yale Neuron has this capability)
        /// </summary>
        /// <param name="V"></param> this is the input voltage
        /// <returns>an</returns> this function returns the rate at the given voltage
        private static Vector an(Vector V)
        {
            Vector Vin = Vector.Build.DenseOfVector(V);
            Vin.Multiply(1.0E3, Vin);
            return (1.0E3) * (0.032) * (15.0 - Vin).PointwiseDivide(((15.0 - Vin) / 5.0).PointwiseExp() - 1.0);
        }
        /// <summary>
        /// This is \f$\beta_n\f$ rate function, the rate functions take the form of
        /// \f[
        /// \frac{a_p(V-B_p)}{\exp(\frac{V-B_p}{C_p})-D_p}
        /// \f]
        /// the constants \f$a_p,B_p,C_p,D_p\f$ are manually coded in for this version of the simulation\n
        /// TODO: come up with an implementation where the user can enter in their own parameters (Yale Neuron has this capability)
        /// </summary>
        /// <param name="V"></param> this is the input voltage
        /// <returns>bn</returns> this function returns the rate at the given voltage
        private static Vector bn(Vector V)
        {
            Vector Vin = Vector.Build.DenseOfVector(V);
            Vin.Multiply(1.0E3, Vin);
            return (1.0E3) * (0.5) * ((10.0 - Vin) / 40.0).PointwiseExp();
        }
        /// <summary>
        /// This is \f$\alpha_m\f$ rate function, the rate functions take the form of
        /// \f[
        /// \frac{a_p(V-B_p)}{\exp(\frac{V-B_p}{C_p})-D_p}
        /// \f]
        /// the constants \f$a_p,B_p,C_p,D_p\f$ are manually coded in for this version of the simulation\n
        /// TODO: come up with an implementation where the user can enter in their own parameters (Yale Neuron has this capability)
        /// </summary>
        /// <param name="V"></param> this is the input voltage
        /// <returns>am</returns> this function returns the rate at the given voltage
        private static Vector am(Vector V)
        {
            Vector Vin = Vector.Build.DenseOfVector(V);
            Vin.Multiply(1.0E3, Vin);
            return (1.0E3) * (0.32) * (13.0 - Vin).PointwiseDivide(((13.0 - Vin) / 4.0).PointwiseExp() - 1.0);
        }
        /// <summary>
        /// This is \f$\beta_m\f$ rate function, the rate functions take the form of
        /// \f[
        /// \frac{a_p(V-B_p)}{\exp(\frac{V-B_p}{C_p})-D_p}
        /// \f]
        /// the constants \f$a_p,B_p,C_p,D_p\f$ are manually coded in for this version of the simulation\n
        /// TODO: come up with an implementation where the user can enter in their own parameters (Yale Neuron has this capability)
        /// </summary>
        /// <param name="V"></param> this is the input voltage
        /// <returns>bm</returns> this function returns the rate at the given voltage
        private static Vector bm(Vector V)
        {
            Vector Vin = Vector.Build.DenseOfVector(V);
            Vin.Multiply(1.0E3, Vin);
            return (1.0E3) * (0.28) * (Vin - 40.0).PointwiseDivide(((Vin - 40.0) / 5.0).PointwiseExp() - 1.0);
        }
        /// <summary>
        /// This is \f$\alpha_h\f$ rate function, the rate functions take the form of
        /// \f[
        /// \frac{a_p(V-B_p)}{\exp(\frac{V-B_p}{C_p})-D_p}
        /// \f]
        /// the constants \f$a_p,B_p,C_p,D_p\f$ are manually coded in for this version of the simulation\n
        /// TODO: come up with an implementation where the user can enter in their own parameters (Yale Neuron has this capability)
        /// </summary>
        /// <param name="V"></param> this is the input voltage
        /// <returns>ah</returns> this function returns the rate at the given voltage
        private static Vector ah(Vector V)
        {
            Vector Vin = Vector.Build.DenseOfVector(V);
            Vin.Multiply(1.0E3, Vin);
            return (1.0E3) * (0.128) * ((17.0 - Vin) / 18.0).PointwiseExp();
        }

        public void AddIonChannel(IonChannel channel)
        {
            ionChannels.Add(channel);

            if (channel.IsActive)
            {
                activeIonChannels.Add(channel);
            }
        }


        public void RemoveIonChannel(IonChannel channel)
        {
            ionChannels.Remove(channel);
            activeIonChannels.Remove(channel);
        }


        public void ActivateIonChannel(IonChannel channel)
        {
            if (!channel.IsActive)
            {
                channel.IsActive = true;
                activeIonChannels.Add(channel);
            }
        }


        public void DeactivateIonChannel(IonChannel channel)
        {
            if (channel.IsActive)
            {
                channel.IsActive = false;
                activeIonChannels.Remove(channel);
            }
        }

        /// <summary>
        /// This is \f$\beta_h\f$ rate function, the rate functions take the form of
        /// \f[
        /// \frac{a_p(V-B_p)}{\exp(\frac{V-B_p}{C_p})-D_p}
        /// \f]
        /// the constants \f$a_p,B_p,C_p,D_p\f$ are manually coded in for this version of the simulation\n
        /// TODO: come up with an implementation where the user can enter in their own parameters (Yale Neuron has this capability)
        /// </summary>
        /// <param name="V"></param> this is the input voltage
        /// <returns>bh</returns> this function returns the rate at the given voltage
        private static Vector bh(Vector V)
        {
            Vector Vin = Vector.Build.DenseOfVector(V);
            Vin.Multiply(1.0E3, Vin);
            return (1.0E3) * 4.0 / (((40.0 - Vin) / 5.0).PointwiseExp() + 1.0);
        }

        // used by save/load functions in Menu.cs
        public double[] getM() { return M.AsArray(); }
        public double[] getN() { return N.AsArray(); }
        public double[] getH() { return H.AsArray(); }

        public double[] getUpre() { return Upre.AsArray(); }
        public double[] getMpre() { return Mpre.AsArray(); }
        public double[] getNpre() { return Npre.AsArray(); }
        public double[] getHpre() { return Hpre.AsArray(); }
        public void BuildVectors(double[] u, double[] m, double[] n, double[] h,
                                    double[] upre, double[] mpre, double[] npre, double[] hpre)
        {
            lock (visualizationValuesLock) U = Vector.Build.DenseOfArray(u);
            lock (visualizationValuesLock) U_Active = U.Clone();
            Upre = Vector.Build.DenseOfArray(upre);

            M = Vector.Build.DenseOfArray(m);
            N = Vector.Build.DenseOfArray(n);
            H = Vector.Build.DenseOfArray(h);

            Mpre = Vector.Build.DenseOfArray(mpre);
            Npre = Vector.Build.DenseOfArray(npre);
            Hpre = Vector.Build.DenseOfArray(hpre);

            Isyn = Vector.Build.Dense(Neuron.nodes.Count, 0.0); // will have to save/load
        }
    }
}