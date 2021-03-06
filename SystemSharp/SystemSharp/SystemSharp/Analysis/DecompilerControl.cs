﻿/**
 * Copyright 2011 Christian Köllner
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
using System.Linq;
using System.Text;
using SystemSharp.Components;
using SystemSharp.SysDOM;

namespace SystemSharp.Analysis
{
    public static class DecompilerControl
    {
        private class BreakDecompilationImpl: RewriteCall
        {
            public override bool Rewrite(Meta.CodeDescriptor decompilee, System.Reflection.MethodBase callee, StackElement[] args, IDecompiler stack, SysDOM.IFunctionBuilder builder)
            {
                stack.Push(LiteralReference.CreateConstant(false), false);
                System.Diagnostics.Debugger.Break();
                return true;
            }
        }

        /// <summary>
        /// This is a debugging aid. This function won't do anything. However, if you call it inside your code, decompilation will
        /// trigger a breakpoint as soon as it meets this method.
        /// </summary>
        /// <returns></returns>
        [BreakDecompilationImpl]
        public static bool BreakDecompilation()
        {
            return false;
        }
    }
}
