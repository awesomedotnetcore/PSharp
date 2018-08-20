﻿//-----------------------------------------------------------------------
// <copyright file="BugTrace.cs">
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

using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;

using Microsoft.PSharp.Runtime;

namespace Microsoft.PSharp.TestingServices.Tracing.Error
{
    /// <summary>
    /// Class implementing a P# bug trace. A trace is a
    /// series of transitions from some initial state to
    /// some end state.
    /// </summary>
    [DataContract]
    public sealed class BugTrace : IEnumerable, IEnumerable<BugTraceStep>
    {
        /// <summary>
        /// The steps of the bug trace.
        /// </summary>
        [DataMember]
        private readonly List<BugTraceStep> Steps;

        /// <summary>
        /// The number of steps in the bug trace.
        /// </summary>
        internal int Count
        {
            get { return this.Steps.Count; }
        }

        /// <summary>
        /// Index for the bug trace.
        /// </summary>
        /// <param name="index">Index</param>
        /// <returns>BugTraceStep</returns>
        internal BugTraceStep this[int index]
        {
            get { return this.Steps[index]; }
            set { this.Steps[index] = value; }
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        internal BugTrace()
        {
            this.Steps = new List<BugTraceStep>();
        }

        /// <summary>
        /// Adds a bug trace step.
        /// </summary>
        /// <param name="mid">The id of the machine.</param>
        /// <param name="machineStateName">The name of the machine state.</param>
        /// <param name="targetMachine">The id of the target machine.</param>
        /// <param name="eventInfo">The event metadata.</param>
        internal void AddCreateMachineStep(IMachineId mid, string machineStateName, IMachineId targetMachine, EventInfo eventInfo)
        {
            var scheduleStep = BugTraceStep.Create(this.Count, BugTraceStepType.CreateMachine,
                mid, machineStateName, eventInfo, null, targetMachine, null, null, null);
            this.Push(scheduleStep);
        }

        /// <summary>
        /// Adds a bug trace step.
        /// </summary>
        /// <param name="monitor">IMachineId</param>
        internal void AddCreateMonitorStep(IMachineId monitor)
        {
            var scheduleStep = BugTraceStep.Create(this.Count, BugTraceStepType.CreateMonitor,
                null, null, null, null, monitor, null, null, null);
            this.Push(scheduleStep);
        }

        /// <summary>
        /// Adds a bug trace step.
        /// </summary>
        /// <param name="machine">The id of the machine.</param>
        /// <param name="machineState">MachineState</param>
        /// <param name="eventInfo">The event metadata.</param>
        /// <param name="targetMachine">The id of the target machine.</param>
        internal void AddSendEventStep(IMachineId machine, string machineState, EventInfo eventInfo, IMachineId targetMachine)
        {
            var scheduleStep = BugTraceStep.Create(this.Count, BugTraceStepType.SendEvent,
                machine, machineState, eventInfo, null, targetMachine, null, null, null);
            this.Push(scheduleStep);
        }

        /// <summary>
        /// Adds a bug trace step.
        /// </summary>
        /// <param name="machine">The id of the machine.</param>
        /// <param name="machineState">MachineState</param>
        /// <param name="eventInfo">The event metadata.</param>
        internal void AddDequeueEventStep(IMachineId machine, string machineState, EventInfo eventInfo)
        {
            var scheduleStep = BugTraceStep.Create(this.Count, BugTraceStepType.DequeueEvent,
                machine, machineState, eventInfo, null, null, null, null, null);
            this.Push(scheduleStep);
        }

        /// <summary>
        /// Adds a bug trace step.
        /// </summary>
        /// <param name="machine">The id of the machine.</param>
        /// <param name="machineState">MachineState</param>
        /// <param name="eventInfo">The event metadata.</param>
        internal void AddRaiseEventStep(IMachineId machine, string machineState, EventInfo eventInfo)
        {
            var scheduleStep = BugTraceStep.Create(this.Count, BugTraceStepType.RaiseEvent,
                machine, machineState, eventInfo, null, null, null, null, null);
            this.Push(scheduleStep);
        }

        /// <summary>
        /// Adds a bug trace step.
        /// </summary>
        /// <param name="machine">The id of the machine.</param>
        /// <param name="machineState">MachineState</param>
        internal void AddGotoStateStep(IMachineId machine, string machineState)
        {
            var scheduleStep = BugTraceStep.Create(this.Count, BugTraceStepType.GotoState,
                machine, machineState, null, null, null, null, null, null);
            this.Push(scheduleStep);
        }

        /// <summary>
        /// Adds a bug trace step.
        /// </summary>
        /// <param name="machine">The id of the machine.</param>
        /// <param name="machineState">MachineState</param>
        /// <param name="action">MethodInfo</param>
        internal void AddInvokeActionStep(IMachineId machine, string machineState, MethodInfo action)
        {
            var scheduleStep = BugTraceStep.Create(this.Count, BugTraceStepType.InvokeAction,
                machine, machineState, null, action, null, null, null, null);
            this.Push(scheduleStep);
        }

        /// <summary>
        /// Adds a bug trace step.
        /// </summary>
        /// <param name="machine">The id of the machine.</param>
        /// <param name="machineState">MachineState</param>
        /// <param name="eventNames">Event names</param>
        internal void AddWaitToReceiveStep(IMachineId machine, string machineState, string eventNames)
        {
            var scheduleStep = BugTraceStep.Create(this.Count, BugTraceStepType.WaitToReceive,
                machine, machineState, null, null, null, null, null, eventNames);
            this.Push(scheduleStep);
        }

        /// <summary>
        /// Adds a bug trace step.
        /// </summary>
        /// <param name="machine">The id of the machine.</param>
        /// <param name="machineState">MachineState</param>
        /// <param name="eventInfo">The event metadata.</param>
        internal void AddReceivedEventStep(IMachineId machine, string machineState, EventInfo eventInfo)
        {
            var scheduleStep = BugTraceStep.Create(this.Count, BugTraceStepType.ReceiveEvent,
                machine, machineState, eventInfo, null, null, null, null, null);
            this.Push(scheduleStep);
        }

        /// <summary>
        /// Adds a bug trace step.
        /// </summary>
        /// <param name="machine">The id of the machine.</param>
        /// <param name="machineState">MachineState</param>
        /// <param name="choice">Choice</param>
        internal void AddRandomChoiceStep(IMachineId machine, string machineState, bool choice)
        {
            var scheduleStep = BugTraceStep.Create(this.Count, BugTraceStepType.RandomChoice,
                machine, machineState, null, null, null, choice, null, null);
            this.Push(scheduleStep);
        }

        /// <summary>
        /// Adds a bug trace step.
        /// </summary>
        /// <param name="machine">The id of the machine.</param>
        /// <param name="machineState">MachineState</param>
        /// <param name="choice">Choice</param>
        internal void AddRandomChoiceStep(IMachineId machine, string machineState, int choice)
        {
            var scheduleStep = BugTraceStep.Create(this.Count, BugTraceStepType.RandomChoice,
                machine, machineState, null, null, null, null, choice, null);
            this.Push(scheduleStep);
        }

        /// <summary>
        /// Adds a bug trace step.
        /// </summary>
        /// <param name="machine">The id of the machine.</param>
        /// <param name="machineState">MachineState</param>
        internal void AddHaltStep(IMachineId machine, string machineState)
        {
            var scheduleStep = BugTraceStep.Create(this.Count, BugTraceStepType.Halt,
                machine, machineState, null, null, null, null, null, null);
            this.Push(scheduleStep);
        }

        /// <summary>
        /// Returns the latest bug trace step and
        /// removes it from the trace.
        /// </summary>
        /// <returns>BugTraceStep</returns>
        internal BugTraceStep Pop()
        {
            if (this.Count > 0)
            {
                this.Steps[this.Count - 1].Next = null;
            }

            var step = this.Steps[this.Count - 1];
            this.Steps.RemoveAt(this.Count - 1);

            return step;
        }

        /// <summary>
        /// Returns the latest bug trace step
        /// without removing it.
        /// </summary>
        /// <returns>BugTraceStep</returns>
        internal BugTraceStep Peek()
        {
            BugTraceStep step = null;

            if (this.Steps.Count > 0)
            {
                step = this.Steps[this.Count - 1];
            }
            
            return step;
        }

        /// <summary>
        /// Returns an enumerator.
        /// </summary>
        /// <returns>IEnumerator</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.Steps.GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator.
        /// </summary>
        /// <returns>IEnumerator</returns>
        IEnumerator<BugTraceStep> IEnumerable<BugTraceStep>.GetEnumerator()
        {
            return this.Steps.GetEnumerator();
        }

        /// <summary>
        /// Pushes a new step to the trace.
        /// </summary>
        /// <param name="step">BugTraceStep</param>
        private void Push(BugTraceStep step)
        {
            if (this.Count > 0)
            {
                this.Steps[this.Count - 1].Next = step;
                step.Previous = this.Steps[this.Count - 1];
            }

            this.Steps.Add(step);
        }
    }
}
