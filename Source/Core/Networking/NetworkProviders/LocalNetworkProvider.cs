﻿//-----------------------------------------------------------------------
// <copyright file="LocalNetworkProvider.cs">
//      Copyright (c) Microsoft Corporation. All rights reserved.
// 
//      THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
//      EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
//      MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
//      IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY
//      CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
//      TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
//      SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// </copyright>
//-----------------------------------------------------------------------

using System;

namespace Microsoft.PSharp.Net
{
    /// <summary>
    /// The local network provider.
    /// </summary>
    internal class LocalNetworkProvider : INetworkProvider
    {
        /// <summary>
        /// Instance of the machine runtime.
        /// </summary>
        private BaseRuntime Runtime;

        /// <summary>
        /// The local endpoint.
        /// </summary>
        private string LocalEndpoint;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="runtime">BaseRuntime</param>
        internal LocalNetworkProvider(BaseRuntime runtime)
        {
            this.Runtime = runtime;
            this.LocalEndpoint = "";
        }

        /// <summary>
        /// Creates a new remote machine of the specified type
        /// and with the specified event. An optional friendly
        /// name can be specified. If the friendly name is null
        /// or the empty string, a default value will be given.
        /// </summary>
        /// <param name="type">Type of the machine</param>
        /// <param name="friendlyName">Friendly machine name used for logging</param>
        /// <param name="endpoint">Endpoint</param>
        /// <param name="e">Event</param>
        /// <returns>MachineId</returns> 
        MachineId INetworkProvider.RemoteCreateMachine(Type type, string friendlyName,
            string endpoint, Event e)
        {
            return this.Runtime.CreateMachine(null, type, friendlyName, e, null, null);
        }

        /// <summary>
        /// Sends an asynchronous event to a machine.
        /// </summary>
        /// <param name="target">Target machine id</param>
        /// <param name="e">Event</param>
        void INetworkProvider.RemoteSend(MachineId target, Event e)
        {
            this.Runtime.SendEvent(target, e, null);
        }

        /// <summary>
        /// Returns the local endpoint.
        /// </summary>
        /// <returns>Endpoint</returns>
        string INetworkProvider.GetLocalEndpoint()
        {
            return this.LocalEndpoint;
        }

        /// <summary>
        /// Disposes the network provider.
        /// </summary>
        public void Dispose() { }
    }
}
