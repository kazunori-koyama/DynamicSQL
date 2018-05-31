using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DynamicSQLParser
{
    /// <summary>
    /// パラメータコード付SQLを表します。
    /// </summary>
    class ParameterSQL : ISQLStatement
    {
        /// <summary>
        /// パラメータコード付SQLなSQLです。
        /// </summary>
        public string SQLString { get; set; }

        public override string ToString()
        {
            return SQLString;
        }
    }
}
