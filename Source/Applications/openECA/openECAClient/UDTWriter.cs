﻿//******************************************************************************************************
//  UDTWriter.cs - Gbtc
//
//  Copyright © 2016, Grid Protection Alliance.  All Rights Reserved.
//
//  Licensed to the Grid Protection Alliance (GPA) under one or more contributor license agreements. See
//  the NOTICE file distributed with this work for additional information regarding copyright ownership.
//  The GPA licenses this file to you under the MIT License (MIT), the "License"; you may
//  not use this file except in compliance with the License. You may obtain a copy of the License at:
//
//      http://opensource.org/licenses/MIT
//
//  Unless agreed to in writing, the subject software distributed under the License is distributed on an
//  "AS-IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. Refer to the
//  License for the specific language governing permissions and limitations.
//
//  Code Modification History:
//  ----------------------------------------------------------------------------------------------------
//  05/25/2016 - Stephen C. Wills
//       Generated original version of source code.
//
//******************************************************************************************************

using System.Collections.Generic;
using System.IO;
using System.Text;
using openECAClient.Model;

namespace openECAClient
{
    /// <summary>
    /// Writes user defined types to files and streams.
    /// </summary>
    public class UDTWriter
    {
        #region [ Members ]

        // Fields
        public List<UserDefinedType> m_types;

        #endregion

        #region [ Constructors ]

        /// <summary>
        /// Creates a new instance of the <see cref="UDTWriter"/> class.
        /// </summary>
        public UDTWriter()
        {
            m_types = new List<UserDefinedType>();
        }

        #endregion

        #region [ Properties ]

        /// <summary>
        /// Gets the list of types to be written to the file.
        /// </summary>
        public List<UserDefinedType> Types
        {
            get
            {
                return m_types;
            }
        }

        #endregion

        #region [ Methods ]

        /// <summary>
        /// Writes the list of UDTs to the given file.
        /// </summary>
        /// <param name="filePath">The path to the file to be written.</param>
        public void Write(string filePath)
        {
            using (TextWriter writer = File.CreateText(filePath))
            {
                Write(writer);
            }
        }

        /// <summary>
        /// Writes the list of UDTs to the given stream.
        /// </summary>
        /// <param name="stream">The stream to which the UDT definitions will be written.</param>
        public void Write(Stream stream)
        {
            using (TextWriter writer = new StreamWriter(stream, Encoding.UTF8, 1024, true))
            {
                Write(writer);
            }
        }

        /// <summary>
        /// Writes the list of UDTs using the given writer.
        /// </summary>
        /// <param name="writer">The writer used to write the UDTs.</param>
        public void Write(TextWriter writer)
        {
            foreach (UserDefinedType type in m_types)
            {
                writer.WriteLine($"category {type.Category}");
                writer.WriteLine($"{type.Identifier} {{");

                foreach (UDTField field in type.Fields)
                    writer.WriteLine($"{field.Type.Category} {field.Type.Identifier} {field.Identifier}");

                writer.WriteLine("}");
                writer.WriteLine();
            }
        }

        #endregion
    }
}