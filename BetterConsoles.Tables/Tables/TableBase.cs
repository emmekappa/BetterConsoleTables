﻿using BetterConsoles.Tables.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Clawfoot.Extensions;
using BetterConsoles.Tables.Models;

namespace BetterConsoles.Tables
{
    // Table class that uses a Type as its rows. Each column is a property on TModel
    /*public abstract class TableBase<TTable, THeader, TModel> : TableBase<TTable, THeader>
        where TTable : TableBase<TTable, THeader, TModel>
    {
        protected List<TModel> m_typedRows;
        public IReadOnlyList<TModel> TypedRows => m_typedRows;

        public abstract TTable AddRow(TModel rowModel);
        public abstract TTable AddRows(IEnumerable<TModel> rowValues);
    }*/


    // Table class that has individual row items of type TCell
    public abstract class TableBase<TTable, THeader, TCell> where TTable : TableBase<TTable, THeader, TCell>
    {
        protected const char paddingChar = ' ';
        protected List<THeader> m_headers;
        protected List<TCell[]> m_rows;
        protected List<List<ICellFormat>> m_formatMatrix;

        public TableBase() : this(new TableConfig()) { }

        public TableBase(TableConfig config)
        {
            Config = config;
            m_headers = new List<THeader>();
            m_rows = new List<TCell[]>();
            m_formatMatrix = new List<List<ICellFormat>>();
        }

        public IReadOnlyList<THeader> Headers => m_headers;
        public IReadOnlyList<TCell[]> Rows => m_rows;
        public TableConfig Config { get; set; }

        /// <summary>
        /// Gets the row with the greatest number of elements
        /// </summary>
        public int LongestRow
        {
            get
            {
                int max = 0;
                for (int i = 0; i < m_rows.Count; i++)
                {
                    max = m_rows[i].Length > max ? m_rows[i].Length : max;
                }
                return max;
            }
        }

        /// <summary>
        /// Adds a row of cell formats based on the column header formats
        /// </summary>
        /// <param name="length"></param>
        protected abstract void AddCellFormatsRow(int length);

        public abstract TTable AddColumn(THeader column);
        public abstract TTable AddColumn(string title, Alignment rowsAlignment = Alignment.Left, Alignment headerAlignment = Alignment.Left);
        public abstract TTable AddColumn(object title, Alignment rowsAlignment = Alignment.Left, Alignment headerAlignment = Alignment.Left);

        /// <summary>
        /// Adds a row to the bottom of the list with the provided column values
        /// Expected that the provided values count is <= the number of columns in the table
        /// </summary>
        /// <param name="values">The column values.</param>
        /// <returns>This Table</returns>
        public virtual TTable AddRow(params TCell[] rowValues)
        {
            if (rowValues is null) throw new ArgumentNullException(nameof(rowValues), "Cannot add a null row to a table");
            if (rowValues.Length == 0) throw new ArgumentException("Cannot add row with a length of 0 to a table", nameof(rowValues));
            if (Headers.Count == 0) throw new InvalidOperationException("No columns exist, please add columns before adding rows");
            if (rowValues.Length > Headers.Count)
            {
                throw new InvalidOperationException(
                    $"The number columns in the row ({rowValues.Length}) is greater than the number of columns in the table ({m_headers.Count})");
            }

            if (rowValues.Length < Headers.Count)
            {
                ResizeRow(ref rowValues, Headers.Count);
            }

            m_rows.Add(rowValues);

            if (m_rows.Count > m_formatMatrix.Count - 1)
            {
                AddCellFormatsRow(rowValues.Length); // Add a new row of formattings
            }

            return (TTable)this;
        }


        /// <summary>
        /// Adds an array of rows to the bottom of the list
        /// </summary>
        /// <param name="rows"></param>
        /// <returns>This Table</returns>
        public virtual TTable AddRows(IEnumerable<TCell[]> rows)
        {
            if (rows is null) throw new ArgumentNullException(nameof(rows), "Cannot add null rows to a table");
            if (!rows.Any()) throw new ArgumentException("Cannot add an empty collection of rows to a table", nameof(rows));

            foreach (var row in rows)
            {
                AddRow(row);
            }

            return (TTable)this;
        }

        /// <summary>
        /// Adds a row to the bottom of the list with the provided column values
        /// Expected that the provided values count is <= the number of columns in the table
        /// </summary>
        /// <param name="values">The column values.</param>
        /// <returns>This Table</returns>
        public abstract TTable AddRow(params object[] rowValues);

        /// <summary>
        /// Adds an array of rows to the bottom of the list
        /// </summary>
        /// <param name="rows"></param>
        /// <returns>This Table</returns>
        public abstract TTable AddRows(IEnumerable<object[]> rows);

        public abstract TTable ReplaceRows(IEnumerable<object[]> rows);


        public abstract string ToString(int[] columnWidths);
        public override abstract string ToString();

        #region Table Generation


        internal protected int[] GetColumnLengths(IColumn[] columns)
        {
            int[] lengths = new int[columns.Length];
            for (int i = 0; i < columns.Length; i++)
            {
                int max = columns[i].Title.Length;
                for (int j = 0; j < m_rows.Count; j++)
                {
                    int length = 0;
                    if (m_formatMatrix[j+1][i].InnerFormatting) // j+1 to skip header format row
                    {
                        length = GetEscapedStringLength(m_rows[j][i]?.ToString());
                    }
                    else
                    {
                        if (i < m_rows[j].Length) // i is in range (ie. row has all columns)
                        {
                            length = m_rows[j][i]?.ToString()?.Length ?? 0;
                        }
                        else
                        {
                            throw new IndexOutOfRangeException();
                        }
                    }

                    if (length > max)
                    {
                        max = length;
                    }
                }
                lengths[i] = max;
            }

            return lengths;
        }

        /// <summary>
        /// Gets the widths of each of the columns of the table
        /// by iterating through each row and determining the width of the longest value
        /// </summary>
        internal protected int[] GetColumnLengths()
        {
            int[] lengths = new int[m_headers.Count];
            for (int i = 0; i < m_headers.Count; i++)
            {
                int max = m_headers[i].ToString().Length;
                for (int j = 0; j < m_rows.Count; j++)
                {
                    int length = 0;
                    if (m_formatMatrix[j + 1][i].InnerFormatting) // j+1 to skip header format row
                    {
                        length = GetEscapedStringLength(m_rows[j][i]?.ToString());
                    }
                    else
                    {
                        if (i < m_rows[j].Length) // i is in range (ie. row has all columns)
                        {
                            length = m_rows[j][i]?.ToString()?.Length ?? 0;
                        }
                        else
                        {
                            throw new IndexOutOfRangeException();
                        }
                    }

                    if (length > max)
                    {
                        max = length;
                    }
                }
                lengths[i] = max;
            }
            return lengths;
        }

        /// <summary>
        /// Gets the length of a string value, excluding formatting sequences
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        internal protected int GetEscapedStringLength(string value)
        {
            if (String.IsNullOrEmpty(value)) return 0;

            bool inEscape = false;
            int totalLen = 0;
            for (int i = 0; i < value.Length; i++)
            {
                // If we hit escape char, and the 2nd escape char to follow matches, we are in an escape point
                if (!inEscape && value[i] == BetterConsoles.Core.Ansi.EscChar1)
                {
                    if (i + 1 < value.Length && value[i + 1] == BetterConsoles.Core.Ansi.EscChar2)
                    {
                        inEscape = true;
                    }
                    continue;
                }

                if (!inEscape)
                {
                    totalLen++;
                }


                if (inEscape && value[i] == 'm')
                {
                    inEscape = false;
                }
            }

            return totalLen;
        }

        /// <summary>
        /// Generates a horizontal dividing row between content rows
        /// </summary>
        /// <param name="columnLengths">The width of each of the columns</param>
        /// <param name="innerDelimiter">The inner intersection divider where vertical and horizontal lines meet</param>
        /// <param name="divider">The horizontal divider</param>
        /// <param name="left">The left outer edge character</param>
        /// <param name="right">The right outer edge character</param>
        /// <returns></returns>
        protected string GenerateDivider(int[] columnLengths, char innerDelimiter, char divider, char left, char right)
        {
            string output = String.Empty;

            output = String.Concat(output, left, String.Empty.PadRight(columnLengths[0] + 2, divider));
            for (int i = 1; i < m_headers.Count; i++)
            {
                output = String.Concat(output, innerDelimiter, String.Empty.PadRight(columnLengths[i] + 2, divider)); //+2 for the 2 spaces around the delimiters
            }
            output = String.Concat(output, right);
            return PadRowInConsole(output);
        }

        /// <summary>
        /// Creates multiple table rows out of the provided column data with column delimiters
        /// </summary>
        /// <param name="values">The values for each column</param>
        /// <param name="columnLengths">The max width of each column</param>
        /// <param name="columnAlignments">The text orientation of each column</param>
        /// <param name="innerDelimiter">The column delimiters for the inside of the table</param>
        /// <param name="outerDelimiter">The column delimiters for the outside edges of the table</param>
        /// <returns></returns>
        protected string[] FormatDataRows(IList<TCell[]> values, int[] columnLengths, Alignment[] columnAlignments, char innerDelimiter, char outerDelimiter)
        {
            string[] output = new string[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                output[i] = FormatDataRow(values[i], columnLengths, columnAlignments, innerDelimiter, outerDelimiter);
            }
            return output;
        }

        /// <summary>
        /// Creates a table row out of the provided column data with column delimiters
        /// </summary>
        /// <param name="values">The values for each column</param>
        /// <param name="columnLengths">The max width of each column</param>
        /// <param name="columnAlignments">The text orientation of each column</param>
        /// <param name="innerDelimiter">The column delimiters for the inside of the table</param>
        /// <param name="outerDelimiter">The column delimiters for the outside edges of the table</param>
        /// <returns></returns>
        protected string FormatDataRow(IList<TCell> values, int[] columnLengths, Alignment[] columnAlignments, char innerDelimiter, char outerDelimiter)
        {
            string output = String.Empty;
            output = String.Concat(output, outerDelimiter, " ", PadString(values[0].ToString(), columnLengths[0], columnAlignments[0]), " ");

            for (int i = 1; i < m_headers.Count; i++)
            {
                output = String.Concat(output, innerDelimiter, " ", PadString(values[i].ToString(), columnLengths[i], columnAlignments[i]), " ");
            }

            output = String.Concat(output, outerDelimiter);
            return PadRowInConsole(output);
        }

        // Same as above but without alignment param
        protected string FormatDataRow(IList<TCell> values, int[] columnLengths, char innerDelimiter, char outerDelimiter)
        {
            string output = String.Empty;
            output = String.Concat(output, outerDelimiter, " ", PadString(values[0].ToString(), columnLengths[0], Alignment.Left), " ");

            for (int i = 1; i < m_headers.Count; i++)
            {
                output = String.Concat(output, innerDelimiter, " ", PadString(values[i].ToString(), columnLengths[i], Alignment.Left), " ");
            }

            output = String.Concat(output, outerDelimiter);
            return PadRowInConsole(output);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Pads a string to the max length following the provided alignment
        /// </summary>
        protected string PadString(string value, int maxLength, Alignment alignment, bool innerFormatting = false)
        {
            //TODO: PERFORMANCE: I need to only do this once and cache it somewhere, like in an array
            // Get the real length and add the difference to the max length so the padding is accurate
            if (innerFormatting)
            {
                int realLength = GetEscapedStringLength(value);
                maxLength = maxLength + (value.Length - realLength);
            }

            if (value.Length == maxLength)
            {
                return value;
            }

            switch (alignment)
            {
                case Alignment.Left:
                    return value.PadRight(maxLength, paddingChar);
                case Alignment.Right:
                    return value.PadLeft(maxLength, paddingChar);
                case Alignment.Center:
                    return value.PadLeftAndRight(maxLength, paddingChar);
                default:
                    throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Pads the row out to the edge of the console, if row is wider then console, expand console window
        /// </summary>
        /// <param name="row"></param>
        /// <returns></returns>
        protected string PadRowInConsole(string renderedRow)
        {
            //No need to, and cannot pad out rows if there is no console
            if (!TableConfig.ConsoleAvailable)
            {
                return renderedRow;
            }

            try
            {
                if (renderedRow.Length < Console.WindowWidth)
                {
                    return renderedRow.PadRight(Console.WindowWidth - 1);
                }
                else
                {
                    if (Config.ExpandConsole && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        Console.WindowWidth = Math.Min(renderedRow.Length + 1, Console.LargestWindowWidth - 1);
                    }
                    return renderedRow;
                }
            }
            catch (IOException ex) //If a console is not available an IOException is thrown
            {
                TableConfig.ConsoleAvailable = false;
                return renderedRow;
            }
        }

        protected string WrapText(string text, int maxWidth)
        {
            return text.Wrap(maxWidth);
        }

        #endregion

        #region State Management

        /// <summary>
        ///     Extends the length of a format row to the provided length, 
        ///     copying the formatting from the matching header
        /// </summary>
        /// <remarks>
        ///     Used when a column is added after the data, which causes empty elements to be added to each row
        ///     to ensure the row lengths match the column lengths. This will cause the format matrix's dimensions
        ///     to no longer be in sync with the data dimensions.
        /// </remarks>
        /// <param name="matrixRowIndex">The row index of the format matrix that needs to be extended</param>
        /// <param name="elementsToAdd">The number of elements to add to the format row</param>
        protected abstract void ExtendFormatRow(int matrixRowIndex, int elementsToAdd);

        /// <summary>
        /// Ensures all rows have format matrix elements for each of their elements
        /// </summary>
        protected virtual void EnsureProperFormatRowSize()
        {
            for (int i = 0; i < m_rows.Count; i++)
            {
                int formatIndex = i + 1; //1st matrix row is for headers

                if (i >= m_formatMatrix.Count)
                {
                    //Need to add a format matrix row
                }

                // Format row is shorter than data row, extend format out to match data length
                if (m_formatMatrix[formatIndex].Count < m_rows[i].Length)
                {
                    int toAdd = m_rows[i].Length - m_formatMatrix[formatIndex].Count;
                    ExtendFormatRow(formatIndex, toAdd);
                }
            }
        }

        /// <summary>
        /// Ensures all rows are the appropriate length for the column count.
        /// Called when a column is added after data exists in the table
        /// </summary>
        protected virtual void EnsureProperRowSize()
        {
            // If there are rows, and the longest row has fewer columns that the current column count
            if (m_rows.Count > 0 && LongestRow < m_headers.Count)
            {
                ResizeAllRows(m_headers.Count); // Extend length of rows
                EnsureProperFormatRowSize(); // Ensure format matrix dimensions match data
            }
        }

        /// <summary>
        /// Increments the length of all row arrays
        /// Sets the new elements to default
        /// </summary>
        /// <param name="increments"></param>
        protected virtual void IncrementRowElements(int increments)
        {
            for (int i = 0; i < m_rows.Count; i++)
            {
                TCell[] array = m_rows[i];
                int length = array.Length;

                Array.Resize(ref array, length + increments);

                m_rows[i] = array;
                for (int j = length; j < m_rows[i].Length; j++)
                {
                    m_rows[i][j] = default(TCell);
                }
            }
        }

        /// <summary>
        /// Sets the sizes of all row arrays to the provided length
        /// </summary>
        /// <param name="newSize"></param>
        protected virtual void ResizeAllRows(int newSize)
        {
            for (int i = 0; i < m_rows.Count; i++)
            {
                if (m_rows[i].Length == newSize)
                {
                    continue;
                }

                TCell[] array = m_rows[i];

                ResizeRow(ref array, newSize);

                m_rows[i] = array;
            }
        }

        /// <summary>
        /// Resizes a row array to a specific length
        /// Sets the new elements to default
        /// </summary>
        /// <param name="row"></param>
        /// <param name="newSize"></param>
        protected virtual void ResizeRow(ref TCell[] row, int newSize)
        {
            int length = row.Length;
            Array.Resize(ref row, newSize);
            Type cellType = typeof(TCell);

            for (int i = length; i < row.Length; i++)
            {
                if (cellType == typeof(string))
                {
                    row[i] = (TCell)(object)String.Empty;
                }
                else
                {
                    row[i] = default(TCell);
                }
            }
        }

        /// <summary>
        /// Resizes a row array to a specific length
        /// Sets the new elements to default
        /// </summary>
        /// <param name="row"></param>
        /// <param name="newSize"></param>
        protected virtual void ResizeRow(ref ICell[] row, int newSize)
        {
            int length = row.Length;
            Array.Resize(ref row, newSize);
            Type cellType = typeof(TCell);

            for (int i = length; i < row.Length; i++)
            {
                if (cellType == typeof(string))
                {
                    row[i] = new Cell<TCell>((TCell)(object)String.Empty);
                }
                else
                {
                    row[i] = new Cell<TCell>(default(TCell));
                }
            }
        }

        #endregion

        #region Reflection

        /// <summary>
        /// Erases existing table data and derives the table from the provided types.
        /// Headers are derived from Property Names
        /// Rows are derived from Property Values
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="items"></param>
        /// <returns></returns>
        public TTable From<T>(T[] items)
        {
            m_rows.Clear();
            m_headers.Clear();

            ProcessReflectionData(items);
            return (TTable)this;
        }

        protected void ProcessReflectionData<T>(T[] genericData)
        {
            PropertyInfo[] properties = typeof(T).GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            string[] columns = GetReflectionHeaders(properties);
            object[][] data = GetReflectionRowsData<T>(genericData, properties);

            foreach (string column in columns)
            {
                AddColumn(column);
            }
            AddRows(data);
        }

        private string[] GetReflectionHeaders(PropertyInfo[] properties)
        {
            string[] output = new string[properties.Length];
            for (int i = 0; i < properties.Length; i++)
            {
                output[i] = properties[i].Name;
            }
            return output;
        }

        private object[][] GetReflectionRowsData<T>(T[] data, PropertyInfo[] properties)
        {
            object[][] output = new object[data.Length][];
            for (int i = 0; i < data.Length; i++)
            {
                object[] values = new object[properties.Length];

                // Is null or default. Value type default is 0, reference types is null
                // If the row is null, fill all row values with the default
                if (EqualityComparer<T>.Default.Equals(data[i], default(T)))
                {
                    object elementValue = String.Empty;
                    // Cannot ToString() null
                    if (default(T) == null)
                    {
                        elementValue = "null";
                    }
                    else
                    {
                        elementValue = default(T);
                    }
                    for (int j = 0; j < properties.Length; j++)
                    {
                        values[j] = elementValue;
                    }

                    continue;
                }

                for (int j = 0; j < properties.Length; j++)
                {
                    object columnValue = properties[j].GetValue(data[i]);

                    if (columnValue is null)
                    {
                        values[j] = "null";
                        continue;
                    }

                    values[j] = columnValue;
                }
                output[i] = values;
            }
            return output;
        }

        #endregion
    }
}
