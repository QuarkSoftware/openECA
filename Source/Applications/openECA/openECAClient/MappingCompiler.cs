﻿//******************************************************************************************************
//  MappingCompiler.cs - Gbtc
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GSF.Annotations;
using GSF.Collections;
using openECAClient.Model;

namespace openECAClient
{
    public class InvalidMappingException : Exception
    {
        public InvalidMappingException(string message)
            : base(message)
        {
        }

        public InvalidMappingException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Compiles user defined types from an IDL into an object
    /// structure that can be used for lookups and serialization.
    /// </summary>
    public class MappingCompiler
    {
        #region [ Members ]

        // Fields
        private UDTCompiler m_udtCompiler;
        private static Dictionary<string, TypeMapping> m_definedMappings;
        private List<InvalidUDTException> m_batchErrors;

        private string m_mappingFile;
        private TextReader m_reader;
        private char m_currentChar;
        private bool m_endOfFile;

        #endregion

        #region [ Constructors ]

        /// <summary>
        /// Creates a new instance of the <see cref="MappingCompiler"/> class.
        /// </summary>
        public MappingCompiler(UDTCompiler udtCompiler)
        {
            m_udtCompiler = udtCompiler;
            m_definedMappings = new Dictionary<string, TypeMapping>();
            m_batchErrors = new List<InvalidUDTException>();
        }

        #endregion

        #region [ Properties ]

        /// <summary>
        /// Gets a list of all the types defined by
        /// UDT definitions parsed by this compiler.
        /// </summary>
        public List<TypeMapping> DefinedTypes
        {
            get
            {
                return m_definedMappings.Values.ToList();
            }
        }

        /// <summary>
        /// Returns a list of errors encountered while parsing
        /// types during a directory scan or type resolution.
        /// </summary>
        public List<InvalidUDTException> BatchErrors
        {
            get
            {
                return m_batchErrors;
            }
        }

        #endregion

        #region [ Methods ]

        /// <summary>
        /// Scans the directory for .ecaidl files and compiles the UDTs defined in them.
        /// </summary>
        /// <param name="directory">The directory to scan for UDTs.</param>
        /// <exception cref="ArgumentNullException"><paramref name="directory"/> is null</exception>
        public void Scan(string directory)
        {
            if ((object)directory == null)
                throw new ArgumentNullException(nameof(directory));

            m_batchErrors.Clear();

            foreach (string idlFile in Directory.EnumerateFiles(directory, "*.ecamap", SearchOption.AllDirectories))
            {
                try
                {
                    Compile(idlFile);
                }
                catch (InvalidUDTException ex)
                {
                    m_batchErrors.Add(ex);
                }
            }
        }

        /// <summary>
        /// Compiles the given mapping file.
        /// </summary>
        /// <param name="mappingFile">The file to be compiled.</param>
        /// <exception cref="ArgumentNullException"><paramref name="mappingFile"/> is null</exception>
        /// <exception cref="InvalidUDTException">An error occurs during compilation.</exception>
        public void Compile(string mappingFile)
        {
            if ((object)mappingFile == null)
                throw new ArgumentNullException(nameof(mappingFile));

            try
            {
                m_mappingFile = mappingFile;

                using (TextReader reader = File.OpenText(mappingFile))
                {
                    Compile(reader);
                }
            }
            finally
            {
                m_mappingFile = null;
            }
        }

        /// <summary>
        /// Compiles UDTs by reading from the given stream.
        /// </summary>
        /// <param name="stream">The stream in which the UDTs are defined.</param>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null</exception>
        public void Compile(Stream stream)
        {
            if ((object)stream == null)
                throw new ArgumentNullException(nameof(stream));

            using (TextReader reader = new StreamReader(stream, Encoding.UTF8, false, 1024, true))
            {
                Compile(reader);
            }
        }

        /// <summary>
        /// Compiles UDTs by reading from the given reader.
        /// </summary>
        /// <param name="reader">The reader used to read the UDT definitions.</param>
        /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null</exception>
        public void Compile(TextReader reader)
        {
            if ((object)reader == null)
                throw new ArgumentNullException(nameof(reader));

            m_reader = reader;

            // Read the first character from the file
            ReadNextChar();
            SkipWhitespace();

            while (!m_endOfFile)
            {
                ParseTypeMapping();
                SkipWhitespace();
            }
        }

        /// <summary>
        /// Gets the type identified by the given identifier.
        /// </summary>
        /// <param name="identifier">The identifier for the data type.</param>
        /// <returns>The data type identified by the identifier or null if no type is found.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="identifier"/> is null</exception>
        /// <remarks>
        /// The first time a user defined type is accessed, the type references
        /// made by that type and all of its referenced types are resolved.
        /// </remarks>
        public TypeMapping GetTypeMapping(string identifier)
        {
            TypeMapping typeMapping;

            if ((object)identifier == null)
                throw new ArgumentNullException(nameof(identifier));

            if (!m_definedMappings.TryGetValue(identifier, out typeMapping))
                return null;

            return typeMapping;
        }

        private void ParseTypeMapping()
        {
            string typeCategory;
            string typeIdentifier;
            DataType mappingType;

            TypeMapping typeMapping;
            Dictionary<string, UDTField> fieldLookup;

            // Assume the first token we encounter is the type identifier
            typeIdentifier = ParseIdentifier();
            SkipWhitespace();

            // Assume the second token we encounter is the mapping identifier
            typeMapping = new TypeMapping();
            typeMapping.Identifier = ParseIdentifier();
            SkipWhitespace();

            // If the next character we encounter is not '{' then
            // the first identifier was the type category,
            // the second identifier was the type identifier
            // the third identifier is the mapping identifier
            if (!m_endOfFile && m_currentChar != '{')
            {
                typeCategory = typeIdentifier;
                typeIdentifier = typeMapping.Identifier;
                typeMapping.Identifier = ParseIdentifier();
                SkipWhitespace();
            }
            else
            {
                typeCategory = null;
            }

            Assert('{');

            // Scan ahead to search
            // for the next newline
            ReadNextChar();
            SkipToNewline();
            Assert('\n');

            // Find the type used for the mapping
            if (!string.IsNullOrEmpty(typeCategory))
                mappingType = m_udtCompiler.GetType(typeCategory, typeIdentifier);
            else
                mappingType = m_udtCompiler.GetType(typeIdentifier);

            if ((object)mappingType == null)
                RaiseCompileError($"Type {typeIdentifier} was not found but is used in the definition for mapping {typeMapping.Identifier}.");

            if (!mappingType.IsUserDefined)
                RaiseCompileError($"Type {typeIdentifier} is not a user defined type but is used in the definition for mapping {typeMapping.Identifier}.");

            typeMapping.Type = (UserDefinedType)mappingType;
            fieldLookup = typeMapping.Type.Fields.ToDictionary(field => field.Identifier);

            // Parse field mappings
            SkipWhitespace();

            while (!m_endOfFile && m_currentChar != '}')
            {
                typeMapping.FieldMappings.Add(ParseFieldMapping(typeMapping, fieldLookup));
                SkipWhitespace();
            }

            Assert('}');
            ReadNextChar();

            // Add the type mapping to the lookup table for defined mappings
            m_definedMappings.Add(typeMapping.Identifier, typeMapping);
        }

        private FieldMapping ParseFieldMapping(TypeMapping typeMapping, Dictionary<string, UDTField> fieldLookup)
        {
            UDTField field;
            FieldMapping fieldMapping;
            string fieldIdentifier;
            string signalID;

            // Read the field identifier
            fieldIdentifier = ParseIdentifier();
            SkipWhitespace();
            Assert(':');
            ReadNextChar();
            SkipWhitespace();

            if (m_endOfFile)
                RaiseCompileError("Unexpected end of file. Expected '{', identifier, or signal ID.");

            if (m_currentChar == '{')
            {
                fieldMapping = ParseCollectionExpression();
            }
            else
            {
                signalID = ParseIdentifier();
                SkipToNewline();

                if (!m_endOfFile && (char.IsDigit(m_currentChar) || m_currentChar == '\n'))
                    fieldMapping = ParseSignalExpression(signalID);
                else
                    fieldMapping = ParseWindowExpression(signalID);
            }

            Assert('\n');

            // Look up the field based on the field identifier
            if (!fieldLookup.TryGetValue(fieldIdentifier, out field))
                RaiseCompileError($"Field {fieldIdentifier} not defined for type {typeMapping.Type.Identifier} but is used in the definition for mapping {typeMapping.Identifier}.");

            fieldMapping.Field = field;

            return fieldMapping;
        }

        private FieldMapping ParseSignalExpression(string expression)
        {
            FieldMapping fieldMapping = new FieldMapping();

            fieldMapping.Expression = expression;

            if (!m_endOfFile && m_currentChar != '\n')
                ParseRelativeTime(fieldMapping);

            return fieldMapping;
        }

        private ArrayMapping ParseCollectionExpression()
        {
            ArrayMapping arrayMapping = new ArrayMapping();
            StringBuilder filterExpression = new StringBuilder();

            do
            {
                ReadNextChar();

                // Scan ahead to the next closing brace
                while (!m_endOfFile && m_currentChar != '}')
                {
                    filterExpression.Append(m_currentChar);
                    ReadNextChar();
                }

                ReadNextChar();

                // Upon encountering two consecutive closing braces,
                // the first brace escapes the second
                if (m_currentChar == '}')
                    filterExpression.Append(m_currentChar);
            }
            while (!m_endOfFile && m_currentChar == '}');

            SkipToNewline();

            if (m_endOfFile)
                RaiseCompileError("Unexpected end of file. Expected number or newline.");

            if (m_currentChar != '\n')
                ParseRelativeTime(arrayMapping);

            arrayMapping.Expression = filterExpression.ToString();

            return arrayMapping;
        }

        private ArrayMapping ParseWindowExpression(string expression)
        {
            ArrayMapping arrayMapping = new ArrayMapping();
            string identifier;

            // Read the next token as an identifier
            arrayMapping.Expression = expression;
            identifier = ParseIdentifier();
            SkipWhitespace();

            if (identifier == "last")
            {
                // The "last" keyword is followed by a
                // timespan and an optional sample rate
                ParseTimeSpan(arrayMapping);
                arrayMapping.RelativeTime = arrayMapping.WindowSize;
                arrayMapping.RelativeUnit = arrayMapping.WindowUnit;
                SkipToNewline();

                // If the sample rate is specified, parse it
                if (!m_endOfFile && m_currentChar == '@')
                    ParseSampleRate(arrayMapping);
            }
            else if (identifier == "from")
            {
                // The from keyword is followed by
                // a relative time and a time span
                ParseRelativeTime(arrayMapping);
                SkipWhitespace();

                // Read the next token as an identifier
                identifier = ParseIdentifier();
                SkipWhitespace();

                // The "for" keyword is expected to exist
                // between the relative time and the time span
                if (identifier != "for")
                    RaiseCompileError($"Unexpected identifier: {identifier}. Expected 'for' keyword.");

                // Parse the time span
                ParseTimeSpan(arrayMapping);
            }
            else
            {
                // If the "last" and "from" keywords are not specified, raise an error
                RaiseCompileError($"Unexpected identifier: {identifier}. Expected 'last' keyword or 'from' keyword.");
            }

            return arrayMapping;
        }

        private string ParseIdentifier()
        {
            StringBuilder builder = new StringBuilder();

            Func<char, bool> isIdentifierChar = c =>
                char.IsLetterOrDigit(c) ||
                c == '_';

            // If the identifier starts with a digit, raise an error
            if (!m_endOfFile && char.IsDigit(m_currentChar))
                RaiseCompileError($"Invalid character for start of identifier: '{GetCharText(m_currentChar)}'. Expected letter or underscore.");
            
            // Read characters as long as they are valid for an identifier
            while (!m_endOfFile && isIdentifierChar(m_currentChar))
            {
                builder.Append(m_currentChar);
                ReadNextChar();
            }

            // If no valid characters were encountered, raise an error
            if (builder.Length == 0)
            {
                if (m_endOfFile)
                    RaiseCompileError($"Unexpected end of file. Expected identifier.");
                else
                    RaiseCompileError($"Unexpected character: '{GetCharText(m_currentChar)}'. Expected identifier.");
            }

            return builder.ToString();
        }

        private void ParseRelativeTime(FieldMapping fieldMapping)
        {
            string identifier;
            TimeSpan timeUnit;

            // Parse the relative time
            fieldMapping.RelativeTime = ParseNumber();
            SkipWhitespace();

            // Parse the next token as an identifier
            identifier = ParseIdentifier();
            timeUnit = ToTimeUnit(identifier);

            if (identifier == "points")
            {
                // The "points" keyword can be
                // followed by an optional sample rate
                SkipToNewline();

                if (!m_endOfFile && m_currentChar == '@')
                    ParseSampleRate(fieldMapping);
            }
            else if (timeUnit != TimeSpan.Zero)
            {
                // If the identifier was a time unit,
                // set RelativeUnit and parse the "ago" keyword
                fieldMapping.RelativeUnit = timeUnit;
                SkipWhitespace();

                identifier = ParseIdentifier();

                if (identifier != "ago")
                    RaiseCompileError($"Unexpected identifier: {identifier}. Expected 'ago' keyword.");
            }
            else if (identifier != "ago")
            {
                // If the identifier was not the ago keyword, raise an error
                RaiseCompileError($"Unexpected identifier: {identifier}. Expected 'points' keyword, 'ago' keyword, or time unit.");
            }
        }

        private void ParseTimeSpan(ArrayMapping arrayMapping)
        {
            // Read the next token as the window size
            arrayMapping.WindowSize = ParseNumber();
            SkipToNewline();

            // Attempt to read the optional time unit
            if (!m_endOfFile && (char.IsLetter(m_currentChar) || m_currentChar == '_'))
            {
                string identifier = ParseIdentifier();
                arrayMapping.WindowUnit = ToTimeUnit(identifier);

                if (arrayMapping.WindowUnit == TimeSpan.Zero)
                    RaiseCompileError($"Unexpected identifier: {identifier}. Expected time unit.");
            }
        }

        private void ParseSampleRate(FieldMapping fieldMapping)
        {
            string identifier;

            // Check for the '@' character
            Assert('@');
            ReadNextChar();
            SkipWhitespace();

            // Parse the next token as the sample rate
            fieldMapping.SampleRate = ParseNumber();
            SkipWhitespace();

            // Parse the "per" keyword
            identifier = ParseIdentifier();
            SkipWhitespace();

            if (identifier != "per")
                RaiseCompileError($"Unexpected identifier: {identifier}. Expected 'per' keyword.");

            // Parse the time unit
            identifier = ParseIdentifier();
            fieldMapping.SampleUnit = ToTimeUnit(identifier);

            if (fieldMapping.SampleUnit == TimeSpan.Zero)
                RaiseCompileError($"Unexpected identifier: {identifier}. Expected time unit.");
        }

        private decimal ParseNumber()
        {
            StringBuilder builder = new StringBuilder();
            decimal number;

            // Read characters as long as they are valid for a number
            while (!m_endOfFile && (char.IsDigit(m_currentChar) || m_currentChar == '.'))
            {
                builder.Append(m_currentChar);
                ReadNextChar();
            }

            // If the characters that were read cannot
            // be interpreted as a number, raise an error
            if (!decimal.TryParse(builder.ToString(), out number))
                RaiseCompileError($"Invalid format for number: '{builder}'.");

            return number;
        }

        private TimeSpan ToTimeUnit(string identifier)
        {
            switch (identifier)
            {
                case "microsecond":
                case "microseconds":
                    return TimeSpan.FromTicks(10L);

                case "millisecond":
                case "milliseconds":
                    return TimeSpan.FromMilliseconds(1.0D);

                case "second":
                case "seconds":
                    return TimeSpan.FromSeconds(1.0D);

                case "minute":
                case "minutes":
                    return TimeSpan.FromMinutes(1.0D);

                case "hour":
                case "hours":
                    return TimeSpan.FromHours(1.0D);

                case "day":
                case "days":
                    return TimeSpan.FromDays(1.0D);

                default:
                    return TimeSpan.Zero;
            }
        }

        private void SkipWhitespace()
        {
            while (!m_endOfFile && char.IsWhiteSpace(m_currentChar))
                ReadNextChar();
        }

        private void SkipToNewline()
        {
            while (!m_endOfFile && char.IsWhiteSpace(m_currentChar) && m_currentChar != '\n')
                ReadNextChar();
        }

        private void ReadNextChar()
        {
            int c = m_reader.Read();

            m_endOfFile = (c == -1);

            if (!m_endOfFile)
                m_currentChar = (char)c;
        }

        private void Assert(params char[] expectedChars)
        {
            Func<char[], string> getExpectedCharsText = chars =>
            {
                StringBuilder builder = new StringBuilder();

                if (chars.Length == 1)
                    return $"'{chars[0]}'";

                if (chars.Length == 2)
                    return $"'{chars[0]}' or '{chars[1]}'";

                for (int i = 0; i < chars.Length; i++)
                {
                    if (i > 0)
                        builder.Append(", ");

                    if (i == chars.Length - 1)
                        builder.Append("or ");

                    builder.Append($"'{chars[i]}'");
                }

                return builder.ToString();
            };

            if (m_endOfFile)
                RaiseCompileError($"Unexpected end of file. Expected {getExpectedCharsText(expectedChars)}");

            if (expectedChars.All(c => m_currentChar != c))
                RaiseCompileError($"Unexpected character: {GetCharText(m_currentChar)}. Expected {getExpectedCharsText(expectedChars)}");
        }

        [ContractAnnotation("=> halt")]
        private void RaiseCompileError(string message)
        {
            if ((object)m_mappingFile == null)
                throw new InvalidMappingException(message);

            string fileName = Path.GetFileName(m_mappingFile);
            string exceptionMessage = $"Error compiling {fileName}: {message}";
            throw new InvalidMappingException(exceptionMessage);
        }

        #endregion

        #region [ Static ]

        // Static Methods

        private static string GetCharText(char c)
        {
            return
                (c == '\r') ? @"\r" :
                (c == '\n') ? @"\n" :
                (c == '\t') ? @"\t" :
                (c == '\0') ? @"\0" :
                c.ToString();
        }

        #endregion
    }
}