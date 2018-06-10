﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PSharp.IO;
using Microsoft.ServiceFabric;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;

namespace Microsoft.PSharp.ReliableServices
{
    /// <summary>
    /// Object hosting an RSM
    /// </summary>
    sealed class BugFindingRsmHost : RsmHost
    {
        /// <summary>
        /// Persistent current state (stack)
        /// </summary>
        public IReliableDictionary<int, string> StateStackStore;

        /// <summary>
        /// Machines that haven't been started yet
        /// </summary>
        public Dictionary<IRsmId, Tuple<string, RsmInitEvent>> PendingMachineCreations;

        /// <summary>
        /// Pending Sends
        /// </summary>
        public List<Tuple<BugFindingRsmId, Event>> PendingSends;

        /// <summary>
        /// Has the machine halted
        /// </summary>
        public bool MachineHalted;

        /// <summary>
        /// Machine failed with an exception
        /// </summary>
        public Exception MachineFailureException;

        /// <summary>
        /// Type of hosted machine
        /// </summary>
        Type MachineType;

        /// <summary>
        /// Initial event of the hosted machine
        /// </summary>
        RsmInitEvent StartingEvent;

        /// <summary>
        /// Action for removing timeout messages
        /// </summary>
        internal Func<string, Task> RemoveSpuriousTimeouts;

        private BugFindingRsmHost(IReliableStateManager stateManager, BugFindingRsmId id, PSharpRuntime runtime)
            : base(stateManager)
        {
            this.Id = id;
            this.PendingMachineCreations = new Dictionary<IRsmId, Tuple<string, RsmInitEvent>>();
            this.PendingSends = new List<Tuple<BugFindingRsmId, Event>>();
            this.Runtime = runtime;
            MachineHalted = false;
            MachineFailureException = null;
        }

        internal static RsmHost Create(IReliableStateManager stateManager, string partitionName, PSharpRuntime runtime)
        {
            var id = new BugFindingRsmId(runtime.CreateMachineId(typeof(BugFindingRsmHostMachine)), partitionName);
            return new BugFindingRsmHost(stateManager, id, runtime);
        }

        internal override RsmHost CreateHost(string partition)
        {
            var id = new BugFindingRsmId(Runtime.CreateMachineId(typeof(BugFindingRsmHostMachine)), partition);
            var host = new BugFindingRsmHost(this.StateManager, id, this.Runtime);
            host.NetworkProvider = NetworkProvider;
            return host;
        }

        public override void SetLogger(ILogger logger)
        {
            Runtime.SetLogger(logger);
        }

        public void SetTransaction(ITransaction tx)
        {
            this.CurrentTransaction = tx;
        }

        private async Task Initialize(Type machineType, RsmInitEvent ev)
        {
            StateStackStore = await StateManager.GetOrAddAsync<IReliableDictionary<int, string>>(string.Format("StateStackStore.{0}", this.Id.Name));
            Timers = await StateManager.GetOrAddAsync<IReliableDictionary<string, Timers.ReliableTimerConfig>>(string.Format("Timers.{0}", this.Id.Name));

            this.MachineType = machineType;
            this.StartingEvent = ev;
            MachineHosted = true;

            var mid = (this.Id as BugFindingRsmId).Mid;
            Runtime.CreateMachine(mid, typeof(BugFindingRsmHostMachine),
                new BugFindingRsmHostMachineInitEvent(this));
        }

        internal async Task InitializationTransaction(Type machineType, RsmInitEvent ev)
        {
            var stack = new List<string>();

            var cnt = await StateStackStore.GetCountAsync(CurrentTransaction);
            if (cnt != 0)
            {
                for (int i = 0; i < cnt; i++)
                {
                    var s = await StateStackStore.TryGetValueAsync(CurrentTransaction, i);
                    stack.Add(s.Value);
                }

                this.Mid = await Runtime.CreateMachineAndExecute(machineType, new ResumeEvent(stack, new RsmInitEvent(this)));
            }
            else
            {
                ev.Host = this;
                this.Mid = await Runtime.CreateMachineAndExecute(machineType, ev);
            }

            if (MachineFailureException != null)
            {
                throw MachineFailureException;
            }

        }

        internal async Task<bool> PersistStateStack()
        {
            if (StackChanges.PopDepth == 0 && StackChanges.PushedSuffix.Count == 0)
            {
                return false;
            }

            var cnt = (int)await StateStackStore.GetCountAsync(CurrentTransaction);
            for (int i = cnt - 1; i > cnt - 1 - StackChanges.PopDepth; i--)
            {
                await StateStackStore.TryRemoveAsync(CurrentTransaction, i);
            }

            for (int i = 0; i < StackChanges.PushedSuffix.Count; i++)
            {
                await StateStackStore.AddAsync(CurrentTransaction, i + (cnt - StackChanges.PopDepth),
                    StackChanges.PushedSuffix[i]);
            }

            return true;
        }

        internal async Task EventHandlerLoop(bool machineRestartRequired, Event ev)
        {
            var eventProcessed = false;

            string lastTimer = null;
            if (ev is Timers.TimeoutEvent)
            {
                lastTimer = (ev as Timers.TimeoutEvent).Name;
            }

            while (true)
            {
                try
                {
                    using (CurrentTransaction = StateManager.CreateTransaction())
                    {
                        SetReliableRegisterTx();

                        if (machineRestartRequired)
                        {
                            machineRestartRequired = false;
                            await InitializationTransaction(MachineType, StartingEvent);
                            await PersistStateStack();
                        }
                        else
                        {
                            await EventHandler(ev);
                            var stackChanged = await PersistStateStack();
                            eventProcessed = true;
                        }

                        await CurrentTransaction.CommitAsync();
                    }

                    if (lastTimer != null && eventProcessed)
                    {
                        if (PendingTimerRemovals.Contains(lastTimer))
                        {
                            // no need to cancel (our timer is a one-time timer)
                            PendingTimerRemovals.Remove(lastTimer);
                        }
                        else
                        {
                            // restart timer
                            PendingTimerCreations.Add(lastTimer,
                                new Timers.ReliableTimerConfig(TimerObjects[lastTimer].Name, TimerObjects[lastTimer].TimePeriod));
                        }
                        TimerObjects.Remove(lastTimer);
                    }

                    StackChanges = new StackDelta();
                    await ExecutePendingWork();
                    
                    if(ev == null || eventProcessed)
                    {
                        break;
                    }
                }
                catch (Exception ex) when (ex is TimeoutException || ex is System.Fabric.TransactionFaultedException)
                {
                    MachineFailureException = null;
                    MachineHalted = false;
                    machineRestartRequired = true;
                    eventProcessed = false;

                    StackChanges = new StackDelta();
                    PendingMachineCreations.Clear();
                    PendingSends.Clear();

                    PendingTimerCreations.Clear();
                    PendingTimerRemovals.Clear();
                }
            }

        }

        /// <summary>
        /// Returns true if dequeued
        /// </summary>
        /// <returns></returns>
        private async Task EventHandler(Event ev)
        {
            await Runtime.SendEventAndExecute(Mid, ev);

            if (MachineFailureException != null)
            {
                throw MachineFailureException;
            }
        }

        internal async Task ExecutePendingWork()
        {
            // timers
            foreach (var tup in PendingTimerCreations)
            {
                var timer = new Timers.ReliableTimerMock((this.Id as BugFindingRsmId).Mid, tup.Value.Period, tup.Value.Name);
                timer.StartTimer();
                TimerObjects.Add(tup.Key, timer);
            }

            PendingTimerCreations.Clear();

            foreach (var name in PendingTimerRemovals)
            {
                if (!TimerObjects.ContainsKey(name))
                {
                    continue;
                }

                var success = TimerObjects[name].StopTimer();
                if(!success)
                {
                    await RemoveSpuriousTimeouts(name);
                }

                TimerObjects.Remove(name);
            }

            PendingTimerRemovals.Clear();

            // machine creations
            foreach (var tup in PendingMachineCreations)
            {
                //var ty = Type.GetType(tup.Value.Item1);

                // Temporary workaround
                var ty = Type.GetType(tup.Value.Item1, 
                    new Func<System.Reflection.AssemblyName, System.Reflection.Assembly>(
                      an => AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName == an.FullName)
                    ), new Func<System.Reflection.Assembly, string, bool, Type>(
                        (a, s, b) => a.GetType(s, false, b)
                    ));

                var host = new BugFindingRsmHost(this.StateManager, tup.Key as BugFindingRsmId, Runtime);
                host.NetworkProvider = this.NetworkProvider;
                await host.Initialize(ty, tup.Value.Item2);
            }

            // send
            foreach (var tup in PendingSends)
            {
                if (tup.Item1.PartitionName == this.Id.PartitionName)
                {
                    Runtime.SendEvent(tup.Item1.Mid, tup.Item2);
                }
                else
                {
                    await this.NetworkProvider.RemoteSend(tup.Item1, tup.Item2);
                }

            }

            PendingMachineCreations.Clear();
            PendingSends.Clear();
        }

        /// <summary>
        /// Creates a fresh Id
        /// </summary>
        /// <typeparam name="T">Machine Type</typeparam>
        /// <returns>Unique Id</returns>
        public override Task<IRsmId> ReliableCreateMachineId<T>()
        {
            return Task.FromResult(new BugFindingRsmId(Runtime.CreateMachineId(typeof(BugFindingRsmHostMachine)), this.Id.PartitionName) as IRsmId);
        }

        /// <summary>
        /// Creates a fresh Id
        /// </summary>
        /// <param name="machineType">Machine Type</typeparam>
        /// <returns>Unique Id</returns>
        public override Task<IRsmId> ReliableCreateMachineId(Type machineType)
        {
            return Task.FromResult(new BugFindingRsmId(Runtime.CreateMachineId(typeof(BugFindingRsmHostMachine)), this.Id.PartitionName) as IRsmId);
        }

        public override async Task<IRsmId> ReliableCreateMachine<T>(RsmInitEvent startingEvent)
        {
            var id = await ReliableCreateMachineId<T>();
            await ReliableCreateMachine(typeof(T), id, startingEvent);
            return id;
        }

        public override async Task<IRsmId> ReliableCreateMachine<T>(RsmInitEvent startingEvent, string partitionName)
        {
            if(partitionName == this.Id.PartitionName)
            {
                return await ReliableCreateMachine<T>(startingEvent);
            }

            var id = await this.NetworkProvider.RemoteCreateMachineId<T>(partitionName);
            await ReliableCreateMachine(typeof(T), id, startingEvent);
            return id;
        }

        /// <summary>
        /// Creates an RSM in the specified partition with the given ID
        /// </summary>
        /// <param name="machineType">Machine Type</typeparam>
        /// <param name="id">ID to attach to the machine</param>
        /// <param name="startingEvent">Starting event for the machine</param>
        public override async Task ReliableCreateMachine(Type machineType, IRsmId id, RsmInitEvent startingEvent)
        {
            if (startingEvent == null)
            {
                //TODO: Specific exception
                throw new Exception("StartingEvent cannot be null");
            }

           // var mid = Runtime.CreateMachineId(typeof(BugFindingRsmHostMachine));
            //var rid = new BugFindingRsmId(mid, this.Id.PartitionName);

            PendingMachineCreations.Add(id, Tuple.Create(machineType.AssemblyQualifiedName, startingEvent));

            if (!MachineHosted)
            {
                await ExecutePendingWork();
            }
        }

        public override async Task ReliableSend(IRsmId target, Event e)
        {
            PendingSends.Add(Tuple.Create(target as BugFindingRsmId, e));

            if (!MachineHosted)
            {
                await ExecutePendingWork();
            }
        }

        internal override void NotifyFailure(Exception ex, string methodName)
        {
            MachineFailureException = ex;
        }

        internal override void NotifyHalt()
        {
            MachineHalted = true;
        }
    }

}
