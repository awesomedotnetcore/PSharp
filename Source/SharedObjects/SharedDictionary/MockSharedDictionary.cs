﻿//-----------------------------------------------------------------------
// <copyright file="MockSharedDictionary.cs">
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
using System.Collections.Generic;

using Microsoft.PSharp.TestingServices;

namespace Microsoft.PSharp.SharedObjects
{
    /// <summary>
    /// A wrapper for a shared dictionary modeled using a state-machine for testing.
    /// </summary>
    internal sealed class MockSharedDictionary<TKey, TValue> : ISharedDictionary<TKey, TValue>
    {
        /// <summary>
        /// Machine modeling the shared dictionary.
        /// </summary>
        MachineId DictionaryMachine;

        /// <summary>
        /// The bug-finding runtime hosting this shared dictionary.
        /// </summary>
        BugFindingRuntime Runtime;

        /// <summary>
        /// Initializes the shared dictionary.
        /// </summary>
        /// <param name="comparer">Comparre for keys</param>
        /// <param name="runtime">BugFindingRuntime</param>
        public MockSharedDictionary(IEqualityComparer<TKey> comparer, BugFindingRuntime runtime)
        {
            this.Runtime = runtime;
            if (comparer != null)
            {
                DictionaryMachine = this.Runtime.CreateMachine(typeof(SharedDictionaryMachine<TKey, TValue>),
                    SharedDictionaryEvent.InitEvent(comparer));
            }
            else
            {
                DictionaryMachine = this.Runtime.CreateMachine(typeof(SharedDictionaryMachine<TKey, TValue>));
            }
        }

        /// <summary>
        /// Adds a new key to the dictionary, if it doesn’t already exist in the dictionary.
        /// </summary>
        /// <param name="key">Key</param>
        /// <param name="value">Value</param>
        /// <returns>True or false depending on whether the new key/value pair was added.</returns>
        public bool TryAdd(TKey key, TValue value)
        {
            var currentMachine = this.Runtime.GetCurrentMachine();
            this.Runtime.SendEvent(DictionaryMachine, SharedDictionaryEvent.TryAddEvent(key, value, currentMachine.Id));
            var e = currentMachine.Receive(typeof(SharedDictionaryResponseEvent<bool>)).Result as SharedDictionaryResponseEvent<bool>;
            return e.Value;
        }

        /// <summary>
        /// Updates the value for an existing key in the dictionary, if that key has a specific value.
        /// </summary>
        /// <param name="key">Key</param>
        /// <param name="newValue">New value</param>
        /// <param name="comparisonValue">Old value</param>
        /// <returns>True if the value with key was equal to comparisonValue and was replaced with newValue; otherwise, false.</returns>
        public bool TryUpdate(TKey key, TValue newValue, TValue comparisonValue)
        {
            var currentMachine = this.Runtime.GetCurrentMachine();
            this.Runtime.SendEvent(DictionaryMachine, SharedDictionaryEvent.TryUpdateEvent(key, newValue, comparisonValue, currentMachine.Id));
            var e = currentMachine.Receive(typeof(SharedDictionaryResponseEvent<bool>)).Result as SharedDictionaryResponseEvent<bool>;
            return e.Value;
        }

        /// <summary>
        /// Attempts to get the value associated with the specified key.
        /// </summary>
        /// <param name="key">Key</param>
        /// <param name="value">Value associated with the key, or the default value if the key does not exist</param>
        /// <returns>True if the key was found; otherwise, false.</returns>
        public bool TryGetValue(TKey key, out TValue value)
        {
            var currentMachine = this.Runtime.GetCurrentMachine();
            this.Runtime.SendEvent(DictionaryMachine, SharedDictionaryEvent.TryGetEvent(key, currentMachine.Id));
            var e = currentMachine.Receive(typeof(SharedDictionaryResponseEvent<Tuple<bool, TValue>>)).Result as SharedDictionaryResponseEvent<Tuple<bool, TValue>>;
            value = e.Value.Item2;
            return e.Value.Item1;
        }

        /// <summary>
        /// Gets or sets the value associated with the specified key.
        /// </summary>
        /// <param name="key">Key</param>
        /// <returns>Value</returns>
        public TValue this[TKey key]
        {
            get
            {
                var currentMachine = this.Runtime.GetCurrentMachine();
                this.Runtime.SendEvent(DictionaryMachine, SharedDictionaryEvent.GetEvent(key, currentMachine.Id));
                var e = currentMachine.Receive(typeof(SharedDictionaryResponseEvent<TValue>)).Result as SharedDictionaryResponseEvent<TValue>;
                return e.Value;
            }
            set
            {
                this.Runtime.SendEvent(DictionaryMachine, SharedDictionaryEvent.SetEvent(key, value));
            }
        }

        /// <summary>
        /// Removes the specified key from the dictionary.
        /// </summary>
        /// <param name="key">Key</param>
        /// <param name="value">Value associated with the key if present, or the default value otherwise.</param>
        /// <returns>True if the element is successfully removed; otherwise, false.</returns>
        public bool TryRemove(TKey key, out TValue value)
        {
            var currentMachine = this.Runtime.GetCurrentMachine();
            this.Runtime.SendEvent(DictionaryMachine, SharedDictionaryEvent.TryRemoveEvent(key, currentMachine.Id));
            var e = currentMachine.Receive(typeof(SharedDictionaryResponseEvent<Tuple<bool, TValue>>)).Result as SharedDictionaryResponseEvent<Tuple<bool, TValue>>;
            value = e.Value.Item2;
            return e.Value.Item1;
        }

        /// <summary>
        /// Gets the number of elements in the dictionary.
        /// </summary>
        /// <returns>Size</returns>
        public int Count
        {
            get
            {
                var currentMachine = this.Runtime.GetCurrentMachine();
                this.Runtime.SendEvent(DictionaryMachine, SharedDictionaryEvent.CountEvent(currentMachine.Id));
                var e = currentMachine.Receive(typeof(SharedDictionaryResponseEvent<int>)).Result as SharedDictionaryResponseEvent<int>;
                return e.Value;
            }
        }
    }
}
