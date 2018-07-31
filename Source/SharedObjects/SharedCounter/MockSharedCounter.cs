﻿//-----------------------------------------------------------------------
// <copyright file="MockSharedCounter.cs">
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

using Microsoft.PSharp.TestingServices;

namespace Microsoft.PSharp.SharedObjects
{
    /// <summary>
    /// A wrapper for a shared counter modeled using a state-machine for testing.
    /// </summary>
    internal sealed class MockSharedCounter : ISharedCounter
    {
        /// <summary>
        /// Machine modeling the shared counter.
        /// </summary>
        private MachineId CounterMachine;

        /// <summary>
        /// The bug-finding runtime hosting this shared counter.
        /// </summary>
        private BugFindingRuntime Runtime;

        /// <summary>
        /// Initializes the shared counter.
        /// </summary>
        /// <param name="value">Initial value</param>
        /// <param name="runtime">BugFindingRuntime</param>
        public MockSharedCounter(int value, BugFindingRuntime runtime)
        {
            this.Runtime = runtime;
            this.CounterMachine = this.Runtime.CreateMachine(typeof(SharedCounterMachine));

            var currentMachine = this.Runtime.GetCurrentMachine();
            this.Runtime.SendEvent(this.CounterMachine, SharedCounterEvent.SetEvent(currentMachine.Id, value));
            currentMachine.Receive(typeof(SharedCounterResponseEvent)).Wait();
        }

        /// <summary>
        /// Increments the shared counter.
        /// </summary>
        public void Increment()
        {
            this.Runtime.SendEvent(this.CounterMachine, SharedCounterEvent.IncrementEvent());
        }

        /// <summary>
        /// Decrements the shared counter.
        /// </summary>
        public void Decrement()
        {
            this.Runtime.SendEvent(this.CounterMachine, SharedCounterEvent.DecrementEvent());
        }

        /// <summary>
        /// Gets the current value of the shared counter.
        /// </summary>
        /// <returns>Current value</returns>
        public int GetValue()
        {
            var currentMachine = this.Runtime.GetCurrentMachine();
            this.Runtime.SendEvent(this.CounterMachine, SharedCounterEvent.GetEvent(currentMachine.Id));
            var response = currentMachine.Receive(typeof(SharedCounterResponseEvent)).Result;
            return (response as SharedCounterResponseEvent).Value;
        }

        /// <summary>
        /// Adds a value to the counter atomically.
        /// </summary>
        /// <param name="value">Value to add</param>
        /// <returns>The new value of the counter</returns>
        public int Add(int value)
        {
            var currentMachine = this.Runtime.GetCurrentMachine();
            this.Runtime.SendEvent(this.CounterMachine, SharedCounterEvent.AddEvent(currentMachine.Id, value));
            var response = currentMachine.Receive(typeof(SharedCounterResponseEvent)).Result;
            return (response as SharedCounterResponseEvent).Value;
        }

        /// <summary>
        /// Sets the counter to a value atomically.
        /// </summary>
        /// <param name="value">Value to set</param>
        /// <returns>The original value of the counter</returns>
        public int Exchange(int value)
        {
            var currentMachine = this.Runtime.GetCurrentMachine();
            this.Runtime.SendEvent(this.CounterMachine, SharedCounterEvent.SetEvent(currentMachine.Id, value));
            var response = currentMachine.Receive(typeof(SharedCounterResponseEvent)).Result;
            return (response as SharedCounterResponseEvent).Value;
        }

        /// <summary>
        /// Sets the counter to a value atomically if it is equal to a given value.
        /// </summary>
        /// <param name="value">Value to set</param>
        /// <param name="comparand">Value to compare against</param>
        /// <returns>The original value of the counter</returns>
        public int CompareExchange(int value, int comparand)
        {
            var currentMachine = this.Runtime.GetCurrentMachine();
            this.Runtime.SendEvent(this.CounterMachine, SharedCounterEvent.CasEvent(currentMachine.Id, value, comparand));
            var response = currentMachine.Receive(typeof(SharedCounterResponseEvent)).Result;
            return (response as SharedCounterResponseEvent).Value;
        }
    }
}
