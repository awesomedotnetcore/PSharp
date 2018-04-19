﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PSharp.ReliableServices
{
    /// <summary>
    /// RsmId implementation for Service Fabric
    /// </summary>
    [DataContract]
    internal class ServiceFabricRsmId : IRsmId
    {
        /// <summary>
        /// Unique value
        /// </summary>
        [DataMember]
        long Value;

        /// <summary>
        /// Name
        /// </summary>
        [DataMember]
        public string Name { get; private set; }

        /// <summary>
        /// Partition hosting the RSM
        /// </summary>
        public string PartitionName { get; private set; }

        /// <summary>
        /// Creates a new ServiceFabricRsmId
        /// </summary>
        /// <param name="value">Unique value</param>
        /// <param name="name">Name</param>
        /// <param name="partitionName">Partition</param>
        internal ServiceFabricRsmId(long value, string name, string partitionName)
        {
            this.Value = value;
            this.Name = string.Format("{0}({1},{2})", name, value, partitionName);
            this.PartitionName = partitionName;
        }

        public int CompareTo(IRsmId other)
        {
            var c = Value.CompareTo((other as ServiceFabricRsmId).Value);
            if (c == 0)
            {
                return PartitionName.CompareTo((other as ServiceFabricRsmId).PartitionName);
            }
            else
            {
                return c;
            }
        }

        public bool Equals(IRsmId other)
        {
            return Value.Equals((other as ServiceFabricRsmId).Value)
                && PartitionName.Equals((other as ServiceFabricRsmId).PartitionName);
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
