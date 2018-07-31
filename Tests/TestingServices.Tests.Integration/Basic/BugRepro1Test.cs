﻿//-----------------------------------------------------------------------
// <copyright file="BugRepro1Test.cs">
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
    public class BugRepro1Test : BaseTest
    {
        class Ping : Event
        {
            public int V;
            public Ping(int v) : base(1, -1) { this.V = v; }
        }

        class Success : Event { }

        class PING : Machine
        {
            int x;
            int y;

            [Start]
            [OnEntry(nameof(EntryPingInit))]
            [OnEventDoAction(typeof(Success), nameof(SuccessAction))]
            [OnEventDoAction(typeof(Ping), nameof(PingAction))]
            class PingInit : MachineState { }

            void EntryPingInit()
            {
                this.Raise(new Success());
            }

            void SuccessAction()
            {
                x = Func1(1, 1);
                this.Assert(x == 2);
                y = Func2(x); // x == 2
            }

            void PingAction()
            {
                this.Assert(x == 4);
                x = x + 1;
                this.Assert(x == 5);
            }

            // i: value passed; j: identifies caller (1: Success handler;  2: Func2)
            int Func1(int i, int j)
            {
                if (j == 1)
                {
                    i = i + 1; // i: 2
                }

                if (j == 2)
                {
                    this.Assert(i == 3);
                    i = i + 1;
                    this.Assert(i == 4);
                    this.Send(this.Id, new Ping(i));
                    this.Assert(i == 4);
                }

                return i;
            }

            int Func2(int v)
            {
                v = v + 1;
                this.Assert(v == 3);
                x = Func1(v, 2);
                this.Assert(x == 4);
                return v;
            }
        }

        [Fact]
        public void TestBugRepro1()
        {
            var test = new Action<IPSharpRuntime>((r) => {
                r.CreateMachine(typeof(PING));
            });

            base.AssertSucceeded(test);
        }
    }
}
