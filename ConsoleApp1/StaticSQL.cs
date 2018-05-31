using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DynamicSQLParser
{
    /// <summary>
    /// 静的なSQLを表します。
    /// </summary>
    class StaticSQL : ISQLStatement
    {
        /// <summary>
        /// 静的なSQLです。
        /// </summary>
        public String SQLString { get; set; }

        public override string ToString()
        {
            return SQLString;
        }
    }
}
