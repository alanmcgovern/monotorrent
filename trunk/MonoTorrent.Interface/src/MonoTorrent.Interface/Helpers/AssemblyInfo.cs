/*
 * $Id: AssemblyInfo.cs 920 2006-08-21 17:32:07Z alanmc $
 * Copyright (c) 2006 by Piotr Wolny <gildur@gmail.com>
 *
 * Permission is hereby granted, free of charge, to any person obtaining a
 * copy of this software and associated documentation files (the "Software"),
 * to deal in the Software without restriction, including without limitation
 * the rights to use, copy, modify, merge, publish, distribute, sublicense,
 * and/or sell copies of the Software, and to permit persons to whom the
 * Software is furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
 * DEALINGS IN THE SOFTWARE.
 */

using System;
using System.Reflection;

namespace MonoTorrent.Interface.Helpers
{
    public static class AssemblyInfo
    {
        private static Assembly assembly;

        static AssemblyInfo()
        {
            assembly = Assembly.GetExecutingAssembly();
        }

        public static String Title {
            get {
                AssemblyTitleAttribute titleAttribute = 
                        (AssemblyTitleAttribute) GetFirstAttribute(
                                typeof (AssemblyTitleAttribute));
                return titleAttribute != null ? titleAttribute.Title : null;
            }
        }

        public static String Copyright {
            get {
                AssemblyCopyrightAttribute copyrightAttribute = 
                        (AssemblyCopyrightAttribute) GetFirstAttribute(
                                typeof (AssemblyCopyrightAttribute));
                return copyrightAttribute != null ? 
                        copyrightAttribute.Copyright : null;
            }
        }

        public static String Version {
            get {
                AssemblyInformationalVersionAttribute versionAttribute = 
                        (AssemblyInformationalVersionAttribute)    
                        GetFirstAttribute(
                                typeof (AssemblyInformationalVersionAttribute));
                return versionAttribute != null ?
                        versionAttribute.InformationalVersion : null;
            }
        }

        private static object GetFirstAttribute(Type type)
        {
            object[] attributes = assembly.GetCustomAttributes(type, false);
            if (attributes.Length > 0) {
                return attributes[0];
            }
            return null;
        }
    }
}
