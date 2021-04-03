﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Cayley
{
    public class Group
    {
        private Graph graph;
        private Tuple<int[], int[]> tree;
        private GroupId groupid;

        // I store the Cayley table. It's O(n^2) memory. The old strategy was complicated and "slow".
        // Keep in mind that n<100
        private int[,] op; // O(n^2)
        private int[] inv; // O(n)
        private int[] orders; // O(n)
        private int[] divisors; // O(n^(1 / log log n))

        public Group(Graph G) : this(G, G.BFS(0)) { }

        public Group(Graph G, Tuple<int[], int[]> T)
        {
            graph = G;
            tree = T;

            writeOperationTables();
            writeOrders();
            checkAbelian();
            checkDihedral();

            // Identify
            groupid = new GroupId(this);
        }

        public int Order { get { return graph.Order; } }
        public int[] Factors { get; private set; }
        public int[] OrderStats { get; private set; }
        public Dictionary<int, int[]> POrderStats { get; private set; }

        public bool IsAbelian { get; private set; }
        public bool IsDihedral { get; private set; }

        public override string ToString()
        {
            return groupid.Identify().ToString();
        }

        // Write the Cayley table
        private void writeOperationTables()
        {
            Factors = DiscreteMath.PrimeFactors(graph.Order);
            divisors = DiscreteMath.Divisors(graph.Order);
            
            Stack<int> element = new Stack<int>();
            op = new int[graph.Order, graph.Order];
            inv = new int[graph.Order];
            for (int i = 0; i < graph.Order; i++)
            {
                element = new Stack<int>();
                int j = i, k;
                while (tree.Item1[j] != -1)
                {
                    k = tree.Item1[j];
                    j = graph.InEdges[j, k];
                    element.Push(k);
                }

                for (j = 0; j < graph.Order; j++)
                {
                    k = j;
                    foreach (int g in element)
                        k = graph.OutEdges[k, g];
                    op[j, i] = k;
                    if (k == 0) inv[j] = i;
                }
            }
        }

        private void writeOrders() {
            // Orders
            // Teeeeechnically, this can be sped up since whenever we find an
            // order, we also know the order of all its powers.
            orders = new int[graph.Order];
            orders[0] = 1;
            for (int i = 0; i < graph.Order; i++)
            {
                int j = op[i, 0], k = 1;
                while (j != 0)
                {
                    j = op[i, j];
                    k++;
                }
                orders[i] = k;
            }

            // Order statistics
            OrderStats = new int[divisors.Length];
            for (int i = 0; i < divisors.Length; i++)
            {
                OrderStats[i] = orders.Count(x => x == divisors[i]);
            }
            POrderStats = new Dictionary<int, int[]>();
            foreach (int p in Factors)
            {
                int multiplicity = MathUtils.Multiplicity(Order, p);
                POrderStats[p] = new int[multiplicity + 1];
                for (int i = 0; i < multiplicity + 1; i++)
                {
                    POrderStats[p][i] = orders.Count(x => x == MathUtils.IntPow(p, i));
                }
            }
        }

        private void checkAbelian() {
            IsAbelian = true;
            for (int i = 0; i < graph.Degree; i++)
            {
                int j = graph.OutEdges[0, i];
                if (!IsCentral(j)) IsAbelian = false;
            }
        }

        /// <summary>
        /// Determine if the group is dihedral.
        /// </summary>
        private void checkDihedral()
        {
            IsDihedral = false;

            // This function *probably * won't be called on abelian groups,
            // but I check for it anyway.
            if (IsAbelian) return;

            // The group order needs to be even.
            if (Factors[0] != 2) return;

            // There needs to be at least two involutions.
            if (OrderStats[1] < 2) return;

            // Since the group isn't abelian, it's definitely dihedral if
            // the order is twice a prime.
            if (Factors.Length == 2)
            {
                IsDihedral = true;
                return;
            }

            // A group is dihedral iff it's generated by two involutions.
            for (int i = 0; i < graph.Order; i++)
            {
                if (orders[i] == 2)
                {
                    for (int j = i + 1; j < graph.Order; j++)
                    {
                        if (orders[j] == 2)
                        {
                            if (isGeneratingSet(new int[] { i, j }))
                            {
                                IsDihedral = true;
                                return;
                            }
                        }
                    }
                }
            }
        }

        private bool isGeneratingSet(int[] v)
        {
            return GeneratedSubgroup(v).Length == Order;
        }

        private int[] GeneratedSubgroup(int[] generatingSet)
        {
            // A quick BFS does the trick
            HashSet<int> subgroup = new HashSet<int>();
            Queue<int> toVisit = new Queue<int>();
            subgroup.Add(0);
            toVisit.Enqueue(0);
            while (toVisit.Count > 0)
            {
                int x = toVisit.Dequeue();
                foreach (int y in generatingSet)
                {
                    if (!subgroup.Contains(op[x, y]))
                    {
                        subgroup.Add(op[x, y]);
                        toVisit.Enqueue(op[x, y]);
                    } 
                }
            }
            return subgroup.ToArray();
        }

        /// <summary>
        /// Returns a power of an element. It's fast exponentiation because multiplications used to be nontrivial
        /// </summary>
        private int pow(int element, int exponent)
        {
            exponent = exponent % orders[element];
            if (exponent < 0) { element = inv[element]; exponent = -exponent; }
            else if (exponent == 0) return 0;

            int t = 0;
            while (exponent > 1)
            {
                if (exponent % 2 == 1) { t = op[element, t]; }
                element = op[element, element];
                exponent >>= 1;
            }
            return op[element, t];
        }

        private int CountPowers(int n)
        {
            HashSet<int> powers = new HashSet<int>();
            for (int i = 0; i < graph.Order; i++)
            {
                powers.Add(pow(i, n));
            }
            return powers.Count();
        }

        /// <summary>
        /// Returns true if the passed element commutes with all other elements in the group.
        /// </summary>
        private bool IsCentral(int a)
        {
            int x;
            for (int i = 0; i < graph.Degree; i++)
            {
                x = graph.OutEdges[0, i];
                if (op[a, x] != op[x, a]) return false;
            }
            return true;
        }

        /// <summary>
        /// Get int array of elements in the derived subgroup (set of all central elements)
        /// </summary>
        private int[] GetCenter()
        {
            // I will probably change how this gets handled, but I really don't want to compute a subgroup unless we have to.
            if (IsAbelian) throw new Exception("Group is abelian");

            List<int> center = new List<int>();
            for (int i = 0; i < graph.Order; i++)
            {
                if (IsCentral(i)) center.Add(i);
            }
            return center.ToArray();
        }

        /// <summary>
        /// Get int array of elements in the derived subgroup (set of all commutators)
        /// </summary>
        private int[] GetDerivedSubgroup()
        {
            HashSet<int> elms = new HashSet<int>();
            elms.Add(0);
            for (int i = 1; i < graph.Order; i++)
            {
                for (int j = 1; j < i; j++)
                {
                    elms.Add(op[op[i, j], inv[op[j, i]]]);
                    elms.Add(op[op[j, i], inv[op[i, j]]]);
                }
            }
            return elms.ToArray();
        }
    }
}
