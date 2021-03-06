﻿/* This code was taken from:
 * http://www.emilstefanov.net/Projects/Files/DisjointSets/DisjointSets.cs
 *
 * It is subject to the MIT license:
 * 
    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in
    all copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
    THE SOFTWARE.
 * */

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace SystemSharp.Collections.EmilStefanov
{
    public class DisjointSets
    {
        /// <summary>
        /// Create an empty DisjointSets data structure
        /// </summary>
        public DisjointSets()
            : this(0)
        {
        }

        /// <summary>
        /// Create a DisjointSets data structure with a specified number of elements (with element id's from 0 to count-1)
        /// </summary>
        /// <param name="count"></param>
        public DisjointSets(int count)
        {
            m_elementCount = 0;
            m_setCount = 0;
            m_nodes = new List<Node>();
            AddElements(count);
        }

        /// <summary>
        /// Find the set identifier that an element currently belongs to.
        /// Note: some internal data is modified for optimization even though this method is consant.
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        public int FindSet(int elementId)
        {
            Contract.Requires(elementId < ElementCount);
            Contract.Requires(elementId >= 0);

            Node curNode;

            // Find the root element that represents the set which `elementId` belongs to
            curNode = m_nodes[elementId];
            while (curNode.Parent != null)
                curNode = curNode.Parent;
            Node root = curNode;

            // Walk to the root, updating the parents of `elementId`. Make those elements the direct
            // children of `root`. This optimizes the tree for future FindSet invokations.
            curNode = m_nodes[elementId];
            while (curNode != root)
            {
                Node next = curNode.Parent;
                curNode.Parent = root;
                curNode = next;
            }

            return root.Index;
        }

        /// <summary>
        /// Combine two sets into one. All elements in those two sets will share the same set id that can be gotten using FindSet.
        /// </summary>
        /// <param name="setId1"></param>
        /// <param name="setId2"></param>
        public void Union(int setId1, int setId2)
        {
            if (setId1 >= m_elementCount)
                throw new ArgumentOutOfRangeException("setId1");
            if (setId2 >= m_elementCount)
                throw new ArgumentOutOfRangeException("setId2");

            if (setId1 == setId2)
                return; // already unioned

            Node set1 = m_nodes[setId1];
            Node set2 = m_nodes[setId2];

            // Determine which node representing a set has a higher rank. The node with the higher rank is
            // likely to have a bigger subtree so in order to better balance the tree representing the
            // union, the node with the higher rank is made the parent of the one with the lower rank and
            // not the other way around.
            if (set1.Rank > set2.Rank)
                set2.Parent = set1;
            else if (set1.Rank < set2.Rank)
                set1.Parent = set2;
            else // set1.Rank == set2.Rank
            {
                set2.Parent = set1;
                ++set1.Rank; // update rank
            }

            // Since two sets have fused into one, there is now one less set so update the set count.
            --m_setCount;
        }

        /// <summary>
        /// Add a specified number of elements to the DisjointSets data structure. The element id's of the new elements are numbered
        /// consequitively starting with the first never-before-used elementId.
        /// </summary>
        /// <param name="addCount"></param>
        public void AddElements(int addCount)
        {
            if (addCount < 0)
                throw new ArgumentOutOfRangeException("addCount");

            // insert and initialize the specified number of element nodes to the end of the `m_nodes` array
            for (int i = m_elementCount; i < m_elementCount + addCount; ++i)
            {
                Node newNode = new Node();
                newNode.Parent = null;
                newNode.Index = i;
                newNode.Rank = 0;
                m_nodes.Add(newNode);
            }

            // update element and set counts
            m_elementCount += addCount;
            m_setCount += addCount;
        }

        /// <summary>
        /// Returns the number of elements currently in the DisjointSets data structure.
        /// </summary>
        public int ElementCount
        {
            get { return m_elementCount; }
        }

        /// <summary>
        /// Returns the number of sets currently in the DisjointSets data structure.
        /// </summary>
        public int SetCount
        {
            get { return m_setCount; }
        }



        /// <summary>
        /// Internal Node data structure used for representing an element.
        /// </summary>
        private class Node
        {
            /// <summary>
            /// This roughly represent the max height of the node in its subtree.
            /// </summary>
            public int Rank;

            /// <summary>
            /// The index of the element the node represents.
            /// </summary>
            public int Index;

            /// <summary>
            /// The parent node of the node.
            /// </summary>
            public Node Parent;
        }

        /// <summary>
        /// The number of elements currently in the DisjointSets data structure.
        /// </summary>
        private int m_elementCount;

        /// <summary>
        /// The number of sets currently in the DisjointSets data structure.
        /// </summary>
        private int m_setCount;

        /// <summary>
        /// The list of nodes representing the elements.
        /// </summary>
        private List<Node> m_nodes;
    }
}
