using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DynamicSQLParser
{
    /// <summary>
    /// /*df if … */ ～ /*ds end if*/ に囲まれた部分を表します。
    /// </summary>
    class IfBlockSQL : ISQLStatement
    {
        /// <summary>
        /// /*df if … */の部分
        /// </summary>
        public string StartIfSQL { get; set; }

        /// <summary>
        /// /*df if … */ ～ /*ds end if*/ の間の部分
        /// </summary>
        public List<ISQLStatement> StatementList { get; set; }

        /// <summary>
        /// /*ds end if*/の部分
        /// </summary>
        public string EndIfSQL { get; set; }

        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.AppendLine(StartIfSQL);

            StatementList.ForEach((e) =>        
            {
                sb.AppendLine(e.ToString());
            });

            sb.AppendLine(EndIfSQL);

            return sb.ToString();
        }
    }
}
