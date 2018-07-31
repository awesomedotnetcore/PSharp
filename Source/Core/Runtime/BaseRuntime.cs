﻿//-----------------------------------------------------------------------
// <copyright file="BaseRuntime.cs">
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

using Microsoft.PSharp.IO;
using Microsoft.PSharp.Net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Microsoft.PSharp
{
    /// <summary>
    /// The base P# runtime.
    /// </summary>
    internal abstract class BaseRuntime : IPSharpRuntime
    {
        /// <summary>
        /// The configuration used by the runtime.
        /// </summary>
        internal Configuration Configuration;

        /// <summary>
        /// Monotonically increasing machine id counter.
        /// </summary>
        internal long MachineIdCounter;

        /// <summary>
        /// Records if the runtime is running.
        /// </summary>
        internal volatile bool IsRunning;

        /// <summary>
        /// Map from unique machine ids to machines.
        /// </summary>
        protected ConcurrentDictionary<MachineId, Machine> MachineMap;

        /// <summary>
        /// Type of Timer machine (used by TMachine)
        /// </summary>
        private Type TimerMachineType = null;

        /// <summary>
        /// Network provider used for remote communication.
        /// </summary>
        public INetworkProvider NetworkProvider { get; private set; }

        /// <summary>
        /// The installed logger.
        /// </summary>
        public ILogger Logger { get; private set; }

        /// <summary>
        /// Event that is fired when the P# program throws an exception.
        /// </summary>
        public event OnFailureHandler OnFailure;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="configuration">Configuration</param>
        protected BaseRuntime(Configuration configuration)
        {
            this.Configuration = configuration;
            this.Initialize();
        }

        /// <summary>
        /// Initializes various components of the runtime.
        /// </summary>
        private void Initialize()
        {
            this.MachineIdCounter = 0;
            this.MachineMap = new ConcurrentDictionary<MachineId, Machine>();
            this.NetworkProvider = new LocalNetworkProvider(this);
            this.SetLogger(new ConsoleLogger());
            this.IsRunning = true;
        }

        #region runtime interface

        /// <summary>
        /// Creates a fresh machine id that has not yet been bound to any machine.
        /// </summary>
        /// <param name="type">Type of the machine</param>
        /// <param name="friendlyName">Friendly machine name used for logging</param>
        /// <returns>MachineId</returns>

        public MachineId CreateMachineId(Type type, string friendlyName = null) => new MachineId(this, type, friendlyName);

        /// <summary>
        /// Creates a new machine of the specified <see cref="Type"/> and with
        /// the specified optional <see cref="Event"/>. This event can only be
        /// used to access its payload, and cannot be handled.
        /// </summary>
        /// <param name="type">Type of the machine</param>
        /// <param name="e">Event</param>
        /// <param name="operationGroupId">Optional operation group id</param>
        /// <returns>MachineId</returns>
        public abstract MachineId CreateMachine(Type type, Event e = null, Guid? operationGroupId = null);

        /// <summary>
        /// Creates a new machine of the specified <see cref="Type"/> and name, and
        /// with the specified optional <see cref="Event"/>. This event can only be
        /// used to access its payload, and cannot be handled.
        /// </summary>
        /// <param name="type">Type of the machine</param>
        /// <param name="friendlyName">Friendly machine name used for logging</param>
        /// <param name="operationGroupId">Optional operation group id</param>
        /// <param name="e">Event</param>
        /// <returns>MachineId</returns>
        public abstract MachineId CreateMachine(Type type, string friendlyName, Event e = null, Guid? operationGroupId = null);

        /// <summary>
        /// Creates a new machine of the specified <see cref="Type"/>, using the specified
        /// machine id, and passes the specified optional <see cref="Event"/>. This
        /// event can only be used to access its payload, and cannot be handled.
        /// </summary>
        /// <param name="mid">Unbound machine id</param>
        /// <param name="type">Type of the machine</param>
        /// <param name="e">Event</param>
        /// <param name="operationGroupId">Optional operation group id</param>
        public abstract void CreateMachine(MachineId mid, Type type, Event e = null, Guid? operationGroupId = null);

        /// <summary>
        /// Sends an asynchronous <see cref="Event"/> to a machine.
        /// </summary>
        /// <param name="target">Target machine id</param>
        /// <param name="e">Event</param>
        /// <param name="options">Optional parameters of a send operation.</param>
        public abstract void SendEvent(MachineId target, Event e, SendOptions options = null);

        /// <summary>
        /// Registers a new specification monitor of the specified <see cref="Type"/>.
        /// </summary>
        /// <param name="type">Type of the monitor</param>
        public abstract void RegisterMonitor(Type type);

        /// <summary>
        /// Invokes the specified monitor with the specified <see cref="Event"/>.
        /// </summary>
        /// <typeparam name="T">Type of the monitor</typeparam>
        /// <param name="e">Event</param>
        public abstract void InvokeMonitor<T>(Event e);

        /// <summary>
        /// Invokes the specified monitor with the specified <see cref="Event"/>.
        /// </summary>
        /// <param name="type">Type of the monitor</param>
        /// <param name="e">Event</param>
        public abstract void InvokeMonitor(Type type, Event e);

        /// <summary>
        /// Returns a nondeterministic boolean choice, that can be controlled
        /// during analysis or testing.
        /// </summary>
        /// <returns>Boolean</returns>
        public bool Random() => this.GetNondeterministicBooleanChoice(null, 2);

        /// <summary>
        /// Returns a nondeterministic boolean choice, that can be controlled
        /// during analysis or testing. The value is used to generate a number
        /// in the range [0..maxValue), where 0 triggers true.
        /// </summary>
        /// <param name="maxValue">Max value</param>
        /// <returns>Boolean</returns>
        public bool Random(int maxValue) => this.GetNondeterministicBooleanChoice(null, maxValue);

        /// <summary>
        /// Returns a nondeterministic integer choice, that can be
        /// controlled during analysis or testing. The value is used
        /// to generate an integer in the range [0..maxValue).
        /// </summary>
        /// <param name="maxValue">Max value</param>
        /// <returns>Integer</returns>
        public int RandomInteger(int maxValue) => this.GetNondeterministicIntegerChoice(null, maxValue);

        /// <summary>
        /// Returns the operation group id of the specified machine. During testing,
        /// the runtime asserts that the specified machine is currently executing.
        /// </summary>
        /// <param name="currentMachine">MachineId of the currently executing machine.</param>
        /// <returns>Guid</returns>
        public abstract Guid GetCurrentOperationGroupId(MachineId currentMachine);

        /// <summary>
        /// Notifies each active machine to halt execution to allow the runtime
        /// to reach quiescence. This is an experimental feature, which should
        /// be used only for testing purposes.
        /// </summary>
        public abstract void Stop();

        #endregion

        #region machine creation and execution

        /// <summary>
        /// Creates a new P# machine of the specified type.
        /// </summary>
        /// <param name="type">Type</param>
        /// <returns>Machine</returns>
        protected Machine CreateMachine(Type type) => MachineFactory.Create(type);

        /// <summary>
        /// Creates a new <see cref="Machine"/> of the specified <see cref="Type"/>.
        /// </summary>
        /// <param name="mid">Unbound machine id</param>
        /// <param name="type">Type of the machine</param>
        /// <param name="friendlyName">Friendly machine name used for logging</param>
        /// <param name="e">Event passed during machine construction</param>
        /// <param name="operationGroupId">Operation group id</param>
        /// <param name="creator">Creator machine</param>
        /// <returns>MachineId</returns>
        protected abstract internal MachineId CreateMachine(MachineId mid, Type type, string friendlyName, Event e, Machine creator, Guid? operationGroupId);

        /// <summary>
        /// Runs a new asynchronous machine event handler.
        /// This is a fire and forget invocation.
        /// </summary>
        /// <param name="machine">Machine that executes this event handler.</param>
        /// <param name="initialEvent">Event for initializing the machine.</param>
        /// <param name="isFresh">If true, then this is a new machine.</param>
        /// <param name="syncCaller">Caller machine that is blocked for quiescence.</param>
        /// <param name="enablingEvent">If non-null, the event info of the sent event that caused the event handler to be restarted.</param>
        protected abstract void RunMachineEventHandler(Machine machine, Event initialEvent, bool isFresh, MachineId syncCaller, EventInfo enablingEvent);

        /// <summary>
        /// Checks that a machine can start its event handler. Returns false if the event
        /// handler should not be started.
        /// </summary>
        /// <param name="machine">Machine</param>
        /// <returns>Boolean</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal virtual bool CheckStartEventHandler(Machine machine) => true;

        /// <summary>
        /// Gets the target machine for an event; if not found, logs a halted-machine entry.
        /// </summary>
        /// <param name="targetMachineId">The id of target machine.</param>
        /// <param name="e">The event that will be sent.</param>
        /// <param name="sender">The machine that is sending the event.</param>
        /// <param name="operationGroupId">The operation group id.</param>
        /// <param name="targetMachine">Receives the target machine, if found.</param>
        protected bool GetTargetMachine(MachineId targetMachineId, Event e, AbstractMachine sender,
            Guid operationGroupId, out Machine targetMachine)
        {
            if (!this.MachineMap.TryGetValue(targetMachineId, out targetMachine))
            {
                var senderState = (sender as Machine)?.CurrentStateName ?? string.Empty;
                this.Logger.OnSend(targetMachineId, sender?.Id, senderState,
                    e.GetType().FullName, operationGroupId, isTargetHalted: true);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if the constructor of the specified machine type exists in the cache.
        /// </summary>
        /// <param name="type">Type</param>
        /// <returns>Boolean</returns>
        protected bool IsMachineConstructorCached(Type type) => MachineFactory.IsCached(type);

        #endregion

        #region timers

        /// <summary>
        /// Overrides the machine type for instantiating timers
        /// </summary>
        /// <param name="timerMachineType">Machine type</param>
        public void SetTimerMachineType(Type timerMachineType)
        {
            this.Assert(timerMachineType.IsSubclassOf(typeof(Machine)), "TimerMachine must be a subclass of Machine");
            this.TimerMachineType = timerMachineType;
        }

        /// <summary>
        /// Return the timer machine type
        /// </summary>
        /// <returns></returns>
        protected internal virtual Type GetTimerMachineType()
        {
            return TimerMachineType;
        }

        #endregion

        #region specifications and error checking

        /// <summary>
        /// Tries to create a new <see cref="PSharp.Monitor"/> of the specified <see cref="Type"/>.
        /// </summary>
        /// <param name="type">Type of the monitor</param>
        protected internal abstract void TryCreateMonitor(Type type);

        /// <summary>
        /// Invokes the specified <see cref="PSharp.Monitor"/> with the specified <see cref="Event"/>.
        /// </summary>
        /// <param name="sender">Sender machine</param>
        /// <param name="type">Type of the monitor</param>
        /// <param name="e">Event</param>
        protected internal abstract void Monitor(Type type, AbstractMachine sender, Event e);

        /// <summary>
        /// Checks if the assertion holds, and if not it throws an
        /// <see cref="AssertionFailureException"/> exception.
        /// </summary>
        /// <param name="predicate">Predicate</param>
        public virtual void Assert(bool predicate)
        {
            if (!predicate)
            {
                throw new AssertionFailureException("Detected an assertion failure.");
            }
        }

        /// <summary>
        /// Checks if the assertion holds, and if not it throws an
        /// <see cref="AssertionFailureException"/> exception.
        /// </summary>
        /// <param name="predicate">Predicate</param>
        /// <param name="s">Message</param>
        /// <param name="args">Message arguments</param>
        public virtual void Assert(bool predicate, string s, params object[] args)
        {
            if (!predicate)
            {
                throw new AssertionFailureException(IO.Utilities.Format(s, args));
            }
        }

        #endregion

        #region nondeterministic choices

        /// <summary>
        /// Returns a nondeterministic boolean choice, that can be
        /// controlled during analysis or testing.
        /// </summary>
        /// <param name="machine">Machine</param>
        /// <param name="maxValue">Max value</param>
        /// <returns>Boolean</returns>
        protected internal abstract bool GetNondeterministicBooleanChoice(AbstractMachine machine, int maxValue);

        /// <summary>
        /// Returns a fair nondeterministic boolean choice, that can be
        /// controlled during analysis or testing.
        /// </summary>
        /// <param name="machine">Machine</param>
        /// <param name="uniqueId">Unique id</param>
        /// <returns>Boolean</returns>
        protected internal abstract bool GetFairNondeterministicBooleanChoice(AbstractMachine machine, string uniqueId);

        /// <summary>
        /// Returns a nondeterministic integer choice, that can be
        /// controlled during analysis or testing.
        /// </summary>
        /// <param name="machine">Machine</param>
        /// <param name="maxValue">Max value</param>
        /// <returns>Integer</returns>
        protected internal abstract int GetNondeterministicIntegerChoice(AbstractMachine machine, int maxValue);

        #endregion

        #region notifications

        /// <summary>
        /// Notifies that a machine entered a state.
        /// </summary>
        /// <param name="machine">Machine</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal virtual void NotifyEnteredState(Machine machine)
        {
            // Override to implement the notification.
        }

        /// <summary>
        /// Notifies that a monitor entered a state.
        /// </summary>
        /// <param name="monitor">Monitor</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal virtual void NotifyEnteredState(Monitor monitor)
        {
            // Override to implement the notification.
        }

        /// <summary>
        /// Notifies that a machine exited a state.
        /// </summary>
        /// <param name="machine">Machine</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal virtual void NotifyExitedState(Machine machine)
        {
            // Override to implement the notification.
        }

        /// <summary>
        /// Notifies that a monitor exited a state.
        /// </summary>
        /// <param name="monitor">Monitor</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal virtual void NotifyExitedState(Monitor monitor)
        {
            // Override to implement the notification.
        }

        /// <summary>
        /// Notifies that a machine invoked an action.
        /// </summary>
        /// <param name="machine">Machine</param>
        /// <param name="action">Action</param>
        /// <param name="receivedEvent">Event</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal virtual void NotifyInvokedAction(Machine machine, MethodInfo action, Event receivedEvent)
        {
            // Override to implement the notification.
        }

        /// <summary>
        /// Notifies that a machine completed invoking an action.
        /// </summary>
        /// <param name="machine">Machine</param>
        /// <param name="action">Action</param>
        /// <param name="receivedEvent">Event</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal virtual void NotifyCompletedAction(Machine machine, MethodInfo action, Event receivedEvent)
        {
            // Override to implement the notification.
        }

        /// <summary>
        /// Notifies that a monitor invoked an action.
        /// </summary>
        /// <param name="monitor">Monitor</param>
        /// <param name="action">Action</param>
        /// <param name="receivedEvent">Event</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal virtual void NotifyInvokedAction(Monitor monitor, MethodInfo action, Event receivedEvent)
        {
            // Override to implement the notification.
        }

        /// <summary>
        /// Notifies that a machine raised an <see cref="Event"/>.
        /// </summary>
        /// <param name="machine">Machine</param>
        /// <param name="eventInfo">EventInfo</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal virtual void NotifyRaisedEvent(Machine machine, EventInfo eventInfo)
        {
            // Override to implement the notification.
        }

        /// <summary>
        /// Notifies that a monitor raised an <see cref="Event"/>.
        /// </summary>
        /// <param name="monitor">Monitor</param>
        /// <param name="eventInfo">EventInfo</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal virtual void NotifyRaisedEvent(Monitor monitor, EventInfo eventInfo)
        {
            // Override to implement the notification.
        }

        /// <summary>
        /// Notifies that a machine dequeued an <see cref="Event"/>.
        /// </summary>
        /// <param name="machine">Machine</param>
        /// <param name="eventInfo">EventInfo</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal virtual void NotifyDequeuedEvent(Machine machine, EventInfo eventInfo)
        {
            // Override to implement the notification.
        }

        /// <summary>
        /// Notifies that a machine invoked pop.
        /// </summary>
        /// <param name="machine">Machine</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal virtual void NotifyPop(Machine machine)
        {
            // Override to implement the notification.
        }

        /// <summary>
        /// Notifies that a machine called Receive.
        /// </summary>
        /// <param name="machine">Machine</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal virtual void NotifyReceiveCalled(Machine machine)
        {
            // Override to implement the notification.
        }

        /// <summary>
        /// Notifies that a machine is handling a raised <see cref="Event"/>.
        /// </summary>
        /// <param name="machine">Machine</param>
        /// <param name="eventInfo">EventInfo</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal virtual void NotifyHandleRaisedEvent(Machine machine, EventInfo eventInfo)
        {
            // Override to implement the notification.
        }

        /// <summary>
        /// Notifies that a machine is waiting to receive one or more events.
        /// </summary>
        /// <param name="machine">Machine</param>
        /// <param name="eventInfoInInbox">The event info if it is in the inbox, else null</param>
        protected internal virtual void NotifyWaitEvents(Machine machine, EventInfo eventInfoInInbox)
        {
            // Override to implement the notification.
        }

        /// <summary>
        /// Notifies that a machine received an <see cref="Event"/> that it was waiting for.
        /// </summary>
        /// <param name="machine">Machine</param>
        /// <param name="eventInfo">EventInfo</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal virtual void NotifyReceivedEvent(Machine machine, EventInfo eventInfo)
        {
            // Override to implement the notification.
        }

        /// <summary>
        /// Notifies that a machine has halted.
        /// </summary>
        /// <param name="machine">Machine</param>
        /// <param name="inbox">Machine inbox.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal virtual void NotifyHalted(Machine machine, LinkedList<EventInfo> inbox)
        {
            // Override to implement the notification.
        }

        /// <summary>
        /// Notifies that the inbox of the specified machine is about to be
        /// checked to see if the default event handler should fire.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal virtual void NotifyDefaultEventHandlerCheck(Machine machine)
        {
            // Override to implement the notification.
        }

        /// <summary>
        /// Notifies that the default handler of the specified machine has been fired.
        /// </summary>
        /// <param name="machine">Machine</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal virtual void NotifyDefaultHandlerFired(Machine machine)
        {
            // Override to implement the notification.
        }

        /// <summary>
        /// Notifies that a machine has progressed. This method can be used to
        /// implement custom notifications based on the specified arguments.
        /// </summary>
        /// <param name="machine">Machine</param>
        /// <param name="args">Arguments</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal virtual Task NotifyProgress(Machine machine, params object[] args)
        {
            // Override to implement the notification.
#if NET45
            return Task.FromResult(0);
#else
            return Task.CompletedTask;
#endif
        }

        #endregion

        #region logging

        /// <summary>
        /// Logs the specified text.
        /// </summary>
        /// <param name="format">Text</param>
        /// <param name="args">Arguments</param>
        protected internal virtual void Log(string format, params object[] args)
        {
            if (this.Configuration.Verbose > 1)
            {
                this.Logger.WriteLine(format, args);
            }
        }

        /// <summary>
        /// Installs the specified <see cref="ILogger"/>.
        /// </summary>
        /// <param name="logger">ILogger</param>
        public void SetLogger(ILogger logger)
        {
            this.Logger = logger ?? throw new InvalidOperationException("Cannot install a null logger.");
            this.Logger.Configuration = this.Configuration;
        }

        /// <summary>
        /// Removes the currently installed <see cref="ILogger"/>, and replaces
        /// it with the default <see cref="ILogger"/>.
        /// </summary>
        public void RemoveLogger()
        {
            this.Logger = new ConsoleLogger();
        }

        #endregion

        #region operation group id

        /// <summary>
        /// Gets the new operation group id to propagate.
        /// </summary>
        /// <param name="sender">Sender machine</param>
        /// <param name="operationGroupId">Operation group id</param>
        /// <returns>Operation group Id</returns>
        internal Guid GetNewOperationGroupId(AbstractMachine sender, Guid? operationGroupId)
        {
            if (operationGroupId.HasValue)
            {
                return operationGroupId.Value;
            }
            else if (sender != null)
            {
                return sender.Info.OperationGroupId;
            }
            else
            {
                return Guid.Empty;
            }
        }

        /// <summary>
        /// Sets the operation group id for the specified machine.
        /// </summary>
        /// <param name="created">Machine created</param>
        /// <param name="sender">Sender machine</param>
        /// <param name="operationGroupId">Operation group id</param>
        internal void SetOperationGroupIdForMachine(Machine created, AbstractMachine sender, Guid? operationGroupId)
        {
            if (operationGroupId.HasValue)
            {
                created.Info.OperationGroupId = operationGroupId.Value;
            }
            else if (sender != null)
            {
                created.Info.OperationGroupId = sender.Info.OperationGroupId;
            }
            else
            {
                created.Info.OperationGroupId = Guid.Empty;
            }
        }

        #endregion

        #region networking

        /// <summary>
        /// Installs the specified <see cref="INetworkProvider"/>.
        /// </summary>
        /// <param name="networkProvider">INetworkProvider</param>
        public void SetNetworkProvider(INetworkProvider networkProvider)
        {
            if (networkProvider == null)
            {
                throw new InvalidOperationException("Cannot install a null network provider.");
            }

            this.NetworkProvider.Dispose();
            this.NetworkProvider = networkProvider;
        }

        /// <summary>
        /// Replaces the currently installed <see cref="INetworkProvider"/>
        /// with the default <see cref="INetworkProvider"/>.
        /// </summary>
        public void RemoveNetworkProvider()
        {
            this.NetworkProvider.Dispose();
            this.NetworkProvider = new LocalNetworkProvider(this);
        }

        #endregion

        #region exceptions

        /// <summary>
        /// Raises the <see cref="OnFailure"/> event with the specified <see cref="Exception"/>.
        /// </summary>
        /// <param name="exception">Exception</param>
        protected internal void RaiseOnFailureEvent(Exception exception)
        {
            if (this.Configuration.AttachDebugger && exception is MachineActionExceptionFilterException &&
                !((exception as MachineActionExceptionFilterException).InnerException is RuntimeException))
            {
                System.Diagnostics.Debugger.Break();
                this.Configuration.AttachDebugger = false;
            }

            this.OnFailure?.Invoke(exception);
        }

        /// <summary>
        /// Throws an <see cref="AssertionFailureException"/> exception
        /// containing the specified exception.
        /// </summary>
        /// <param name="exception">Exception</param>
        /// <param name="s">Message</param>
        /// <param name="args">Message arguments</param>
        protected internal virtual void WrapAndThrowException(Exception exception, string s, params object[] args)
        {
            throw (exception is AssertionFailureException)
                ? exception
                : new AssertionFailureException(IO.Utilities.Format(s, args), exception);
        }

        #endregion

        #region cleanup

        /// <summary>
        /// Disposes runtime resources.
        /// </summary>
        public virtual void Dispose()
        {
            this.MachineIdCounter = 0;
            this.NetworkProvider.Dispose();
        }

        #endregion
    }
}
