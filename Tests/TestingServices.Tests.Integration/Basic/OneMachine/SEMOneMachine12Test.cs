﻿//-----------------------------------------------------------------------
// <copyright file="SEMOneMachine12Test.cs">
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

using Xunit;

namespace Microsoft.PSharp.TestingServices.Tests.Integration
{
    public class SEMOneMachine12Test : BaseTest
    {
        class E1 : Event
        {
            public E1() : base(1, -1) { }
        }

        class E2 : Event
        {
            public E2() : base(1, -1) { }
        }

        class E3 : Event
        {
            public E3() : base(1, -1) { }
        }

        class Real1 : Machine
        {
            bool test = false;

            [Start]
            [OnEntry(nameof(EntryInit))]
            [OnExit(nameof(ExitInit))]
            [OnEventPushState(typeof(E1), typeof(S1))]
            [OnEventDoAction(typeof(E3), nameof(Action1))]
            class Init : MachineState { }

            void EntryInit()
            {
                this.Send(this.Id, new E1());
            }

            void ExitInit()
            {
                this.Send(this.Id, new E2()); // never executed
            }

            [OnEntry(nameof(EntryS1))]
            class S1 : MachineState { }

            void EntryS1()
            {
                test = true;
                this.Send(this.Id, new E3());
                this.Pop();
            }

            void Action1()
            {
                this.Assert(test == false);  // reachable
            }
        }

        /// <summary>
        /// P# semantics test: one machine, "push" transition, inherited action
        /// is not executed in the pushed state, due to the explicit "pop".
        /// This test checks explicit popping of a state.
        /// </summary>
        [Fact]
        public void TestPushExplicitPop()
        {
            var test = new Action<IPSharpRuntime>((r) => {
                r.CreateMachine(typeof(Real1));
            });

            base.AssertFailed(test, 1);
        }
    }
}
