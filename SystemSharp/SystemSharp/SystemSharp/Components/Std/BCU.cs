﻿/**
 * Copyright 2012-2013 Christian Köllner
 * 
 * This file is part of System#.
 *
 * System# is free software: you can redistribute it and/or modify it under 
 * the terms of the GNU Lesser General Public License (LGPL) as published 
 * by the Free Software Foundation, either version 3 of the License, or (at 
 * your option) any later version.
 *
 * System# is distributed in the hope that it will be useful, but WITHOUT ANY
 * WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS 
 * FOR A PARTICULAR PURPOSE. See the GNU General Public License for more 
 * details.
 *
 * You should have received a copy of the GNU General Public License along 
 * with System#. If not, see http://www.gnu.org/licenses/lgpl.html.
 * */

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using SystemSharp.Assembler;
using SystemSharp.Components.FU;
using SystemSharp.Components.Transactions;
using SystemSharp.DataTypes;
using SystemSharp.Meta;
using SystemSharp.Synthesis;

namespace SystemSharp.Components.Std
{
    /// <summary>
    /// Transaction site interface of branch control unit (BCU)
    /// </summary>
    public interface IBCUTransactionSite: ITransactionSite
    {
        /// <summary>
        /// Returns a transaction for an unconditional branch
        /// </summary>
        /// <param name="target">branch target</param>
        IEnumerable<TAVerb> Branch(BranchLabel target);

        /// <summary>
        /// Returns a transaction for a conditional branch: "branch if some condition is true"-style
        /// </summary>
        /// <param name="cond">condition source</param>
        /// <param name="target">branch target</param>
        IEnumerable<TAVerb> BranchIf(ISignalSource<StdLogicVector> cond, BranchLabel target);

        /// <summary>
        /// Returns a transaction for a conditional branch: "branch if some condition is false"-style
        /// </summary>
        /// <param name="cond">condition source</param>
        /// <param name="target">branch target</param>
        IEnumerable<TAVerb> BranchIfNot(ISignalSource<StdLogicVector> cond, BranchLabel target);
    }

    /// <summary>
    /// Implements a service for mapping XIL instructions to branch control units (i.e. instances of BCU).
    /// </summary>
    public class BCUMapper : IXILMapper
    {
        private abstract class BCUMapping : IXILMapping
        {
            protected BCU _bcu;
            protected BranchLabel _target;

            public BCUMapping(BCU bcu, BranchLabel target)
            {
                _bcu = bcu;
                _target = target;
            }

            public abstract IEnumerable<TAVerb> Realize(ISignalSource<StdLogicVector>[] operands, ISignalSink<StdLogicVector>[] results);
            public abstract string Description { get; }

            public ITransactionSite TASite
            {
                get { return _bcu.TASite; }
            }

            public EMappingKind ResourceKind
            {
                get { return EMappingKind.ExclusiveResource; }
            }

            public int InitiationInterval
            {
                get { return 1; }
            }

            public int Latency
            {
                get { return _bcu.Latency; }
            }
        }

        private class GotoMapping : BCUMapping
        {
            public GotoMapping(BCU bcu, BranchLabel target):
                base(bcu, target)
            {
            }

            public override IEnumerable<TAVerb> Realize(ISignalSource<StdLogicVector>[] operands, ISignalSink<StdLogicVector>[] results)
            {
                return _bcu.TASite.Branch(_target);
            }

            public override string Description
            {
                get { return "BCU: goto"; }
            }
        }

        private class BranchIfMapping: BCUMapping
        {
            public BranchIfMapping(BCU bcu, BranchLabel target) :
                base(bcu, target)
            {
            }

            public override IEnumerable<TAVerb> Realize(ISignalSource<StdLogicVector>[] operands, ISignalSink<StdLogicVector>[] results)
            {
                return _bcu.TASite.BranchIf(operands[0], _target);
            }

            public override string Description
            {
                get { return "BCU: conditional branch (positive)"; }
            }
        }

        private class BranchIfNotMapping : BCUMapping
        {
            public BranchIfNotMapping(BCU bcu, BranchLabel target):
                base(bcu, target)
            {
            }

            public override IEnumerable<TAVerb> Realize(ISignalSource<StdLogicVector>[] operands, ISignalSink<StdLogicVector>[] results)
            {
                return _bcu.TASite.BranchIfNot(operands[0], _target);
            }

            public override string Description
            {
                get { return "BCU: conditional branch (negative)"; }
            }
        }

        private BCU _host;
        private int _latency;

        /// <summary>
        /// Constructs an instance
        /// </summary>
        /// <param name="host">the branch control unit</param>
        /// <param name="latency">desired latency</param>
        public BCUMapper(BCU host, int latency = 1)
        {
            _host = host;
            _latency = latency;
        }

        /// <summary>
        /// Returns goto, brtrue, brfalse
        /// </summary>
        public IEnumerable<XILInstr> GetSupportedInstructions()
        {
            yield return DefaultInstructionSet.Instance.Goto(null);
            yield return DefaultInstructionSet.Instance.BranchIfTrue(null);
            yield return DefaultInstructionSet.Instance.BranchIfFalse(null);
        }

        public IEnumerable<IXILMapping> TryMap(ITransactionSite taSite, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes)
        {
            var fu = taSite.Host;
            BCU bcu = fu as BCU;
            if (bcu != _host)
                yield break;

            switch (instr.Name)
            {
                case InstructionCodes.Goto:
                case InstructionCodes.BranchIfTrue:
                case InstructionCodes.BranchIfFalse:
                    {
                        var target = (BranchLabel)instr.Operand;
                        switch (instr.Name)
                        {
                            case InstructionCodes.Goto:
                                yield return new GotoMapping(bcu, target);
                                yield break;

                            case InstructionCodes.BranchIfTrue:
                                yield return new BranchIfMapping(bcu, target);
                                yield break;

                            case InstructionCodes.BranchIfFalse:
                                yield return new BranchIfNotMapping(bcu, target);
                                yield break;

                            default:
                                throw new NotImplementedException();
                        }
                    }

                default:
                    yield break;
            }
        }

        public IXILMapping TryAllocate(Component host, XILInstr instr, TypeDescriptor[] operandTypes, TypeDescriptor[] resultTypes, IProject targetProject)
        {
            switch (instr.Name)
            {
                case InstructionCodes.Goto:
                case InstructionCodes.BranchIfTrue:
                case InstructionCodes.BranchIfFalse:
                    return TryMap(_host.TASite, instr, operandTypes, resultTypes).Single();

                default:
                    return null;
            }
        }
    }

    /// <summary>
    /// A synthesizable implementation of a branch control unit. It is intended to be used during high-level synthesis
    /// for mapping branch instructions.
    /// </summary>
    /// <remarks>
    /// The purpose of a branch control unit (BCU) is to drive the output address towards program memory. If we consider a
    /// conditional branch, there are two possibilities: either the branch is taken or not. Therefore, we have to choose between
    /// two possible addresses of next instruction. One of them is just the address of the physically next instruction,
    /// the other one may be arbitrary. Therefore, the BCU provides an "alternative address" input (<c>AltAddr</c>) and branch
    /// flags (<c>BrP</c> and <c>BrN</c>) which indicate whether to take the branch or not in positive and negative logic, respectively.
    /// From this information, the BCU computes the next output address based on the following truth table:
    /// <list type="table">
    /// <listheader>
    /// <description><c>Rst</c> value</description>
    /// <description><c>BrP</c> value</description>
    /// <description><c>BrN</c> value</description>
    /// <description>Output address <c>OutAddr</c></description>
    /// </listheader>
    /// <item>
    /// <description>1</description>
    /// <description>-</description>
    /// <description>-</description>
    /// <description><c>StartupAddr</c> value</description>
    /// </item>
    /// <item>
    /// <description>0</description>
    /// <description>0</description>
    /// <description>1</description>
    /// <description>last output address + 1</description>
    /// </item>
    /// <item>
    /// <description>0</description>
    /// <description>1</description>
    /// <description>-</description>
    /// <description><c>AltAddr</c> value</description>
    /// </item>
    /// <item>
    /// <description>0</description>
    /// <description>-</description>
    /// <description>0</description>
    /// <description><c>AltAddr</c> value</description>
    /// </item>
    /// </list>
    /// The BCU can account for program ROM latency in that it ignores <c>BrP</c> and <c>BrN</c> during the first 
    /// <c>Latency</c> c-steps after reset.
    /// </remarks>
    public class BCU: Component
    {
        private class BCUTransactionSite : 
            DefaultTransactionSite,
            IBCUTransactionSite
        {
            private BCU _host;

            public BCUTransactionSite(BCU host) :
                base(host)
            {
                _host = host;
            }

            private TAVerb NopVerb()
            {
                return Verb(ETVMode.Locked,
                        _host.BrP.Dual.Drive(SignalSource.Create<StdLogicVector>("0")),
                        _host.BrN.Dual.Drive(SignalSource.Create<StdLogicVector>("1")),
                        _host.AltAddr.Dual.Drive(SignalSource.Create(StdLogicVector._0s(_host.AddrWidth))));
            }

            public override IEnumerable<TAVerb> DoNothing()
            {
                yield return NopVerb();
            }

            public IEnumerable<TAVerb> Branch(BranchLabel target)
            {
                yield return Verb(ETVMode.Locked,
                    _host.BrP.Dual.Drive(SignalSource.Create<StdLogicVector>("1")),
                    _host.BrN.Dual.Drive(SignalSource.Create<StdLogicVector>("0")),
                    _host.AltAddr.Dual.Drive(
                        SignalSource.Create(
                            StdLogicVector.FromUInt(
                                (uint)target.CStep, _host.AddrWidth))));
                for (int i = 1; i < _host.Latency; i++)
                    yield return NopVerb();
            }

            public IEnumerable<TAVerb> BranchIf(ISignalSource<StdLogicVector> cond, BranchLabel target)
            {
                yield return Verb(ETVMode.Locked,
                    _host.BrP.Dual.Drive(cond),
                    _host.BrN.Dual.Drive(SignalSource.Create<StdLogicVector>("1")),
                    _host.AltAddr.Dual.Drive(
                        SignalSource.Create(
                            StdLogicVector.FromUInt(
                                (uint)target.CStep, _host.AddrWidth))));
                for (int i = 1; i < _host.Latency; i++)
                    yield return NopVerb();
            }

            public IEnumerable<TAVerb> BranchIfNot(ISignalSource<StdLogicVector> cond, BranchLabel target)
            {
                yield return Verb(ETVMode.Locked,
                    _host.BrP.Dual.Drive(SignalSource.Create<StdLogicVector>("0")),
                    _host.BrN.Dual.Drive(cond),
                    _host.AltAddr.Dual.Drive(
                        SignalSource.Create(
                            StdLogicVector.FromUInt(
                                (uint)target.CStep, _host.AddrWidth))));
                for (int i = 1; i < _host.Latency; i++)
                    yield return NopVerb();
            }
        }

        /// <summary>
        /// Clock signal input
        /// </summary>
        public In<StdLogic> Clk { private get; set; }

        /// <summary>
        /// Synchronous reset input
        /// </summary>
        public In<StdLogic> Rst { private get; set; }

        /// <summary>
        /// Positive branch flag input
        /// </summary>
        public In<StdLogicVector> BrP { internal get; set; }

        /// <summary>
        /// Negative branch flag input
        /// </summary>
        public In<StdLogicVector> BrN { internal get; set; }

        /// <summary>
        /// Branch address
        /// </summary>
        public In<StdLogicVector> AltAddr { internal get; set; }

        /// <summary>
        /// Address output
        /// </summary>
        public Out<StdLogicVector> OutAddr { internal get; set; }

        /// <summary>
        /// Address width
        /// </summary>
        [PerformanceRelevant]
        public int AddrWidth 
        { 
            [StaticEvaluation] get; [AssumeNotCalled] set; 
        }

        /// <summary>
        /// Startup (reset) address
        /// </summary>
        public StdLogicVector StartupAddr 
        { 
            [StaticEvaluation] get; [AssumeNotCalled] set; 
        }

        /// <summary>
        /// Latency of program ROM. The BCU ignores <c>BrP</c> and <c>BrN</c> during the first <c>Latency</c> clocks after reset.
        /// </summary>
        [PerformanceRelevant]
        public int Latency
        {
            [StaticEvaluation] get;
            private set;
        }

        /// <summary>
        /// Associated transaction site
        /// </summary>
        public IBCUTransactionSite TASite { get; private set; }

        private SLVSignal _lastAddr;
        private SLVSignal _outAddr;
        private SLVSignal _rstq;
        private StdLogicVector _rstPat;

        /// <summary>
        /// Constructs an instance
        /// </summary>
        /// <param name="latency">Latency of program ROM, at least 1</param>
        public BCU(int latency = 1)
        {
            Contract.Requires<ArgumentOutOfRangeException>(latency >= 1, "latency <= 0 does not make sense.");

            TASite = new BCUTransactionSite(this);
            Latency = latency;
        }

        private void ComputeOutAddr()
        {
            if (Rst.Cur == '1')
            {
                _outAddr.Next = StartupAddr;
            }
            else if (BrP.Cur == "1" || BrN.Cur == "0")
            {
                _outAddr.Next = AltAddr.Cur;
            }
            else
            {
                _outAddr.Next =
                    (_lastAddr.Cur.UnsignedValue + 
                    Unsigned.FromUInt(1, AddrWidth))
                    .Resize(AddrWidth).SLVValue;
            }
        }

        private void ComputeOutAddrWithRstQ()
        {
            if (Rst.Cur == '1')
            {
                _outAddr.Next = StartupAddr;
            }
            else if (_rstq.Cur[0] == '1' || (BrP.Cur != "1" && BrN.Cur != "0"))
            {
                _outAddr.Next =
                    (_lastAddr.Cur.UnsignedValue +
                    Unsigned.FromUInt(1, AddrWidth))
                    .Resize(AddrWidth).SLVValue;
            }
            else
            {
                _outAddr.Next = AltAddr.Cur;
            }
        }

        private void UpdateAddr()
        {
            if (Clk.RisingEdge())
            {
                _lastAddr.Next = _outAddr.Cur;
            }
        }

        private void SyncResetHandling()
        {
            if (Clk.RisingEdge())
            {
                if (Rst.Cur == '1')
                    _rstq.Next = _rstPat;
                else
                    _rstq.Next = StdLogic._0.Concat(_rstq.Cur[Latency - 2, 1]);
            }
        }

        private void DriveOutAddrComb()
        {
            OutAddr.Next = _outAddr.Cur;
        }

        private void DriveOutAddrDeferred()
        {
            OutAddr.Next = _lastAddr.Cur;
        }

        protected override void PreInitialize()
        {
            if (StartupAddr.Size != AddrWidth)
                throw new InvalidOperationException("BCU: Invalid startup address");

            _lastAddr = new SLVSignal(StdLogicVector._0s(AddrWidth));
            _outAddr = new SLVSignal(AddrWidth);

            if (Latency > 1)
            {
                _rstPat = StdLogicVector._1s(Latency - 1);
                _rstq = new SLVSignal(_rstPat);
            }
        }

        protected override void Initialize()
        {
            AddProcess(UpdateAddr, Clk);
            AddProcess(DriveOutAddrComb, _outAddr);
            if (Latency > 1)
            {
                AddProcess(SyncResetHandling, Clk);
                AddProcess(ComputeOutAddrWithRstQ, Rst, _rstq, BrP, BrN, AltAddr, _lastAddr);
            }
            else
            {
                AddProcess(ComputeOutAddr, Rst, BrP, BrN, AltAddr, _lastAddr);
            }
        }
    }
}
