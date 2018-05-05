using ConsoleApp1.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    //''' <summary>
    //''' 動的SQLパーサー
    //''' </summary>
    //''' <remarks></remarks>
    public class DynamicSQLParser
    {

        //''' <summary>
        //''' DBコマンドを作成します
        //''' </summary>
        //''' <param name="cn">DBコネクション</param>
        //''' <param name="dsql">DynamicSQL</param>
        //''' <param name="prefix">パラメータ接頭語</param>
        //''' <param name="params">パラメータ値辞書</param>
        //''' <returns></returns>
        //''' <remarks></remarks>
        IDbCommand CreateDbCommand(IDbConnection cn, string dsql, string prefix, Dictionary<string, Object> param)
        {
            IDbCommand cmd = cn.CreateCommand();

            //'パラメータ追加
            Action<string, Object> paramAdder = (name, value) =>
            {
                if (cmd.Parameters.Contains(name))
                {
                    return;
                }
                IDbDataParameter prm = cmd.CreateParameter();
                prm.ParameterName = name;
                prm.Value = value ?? DBNull.Value;
                cmd.Parameters.Add(prm);
            };

            //Sub(name As String, value As Object)
            //    If cmd.Parameters.Contains(name) Then Return
            //    Dim prm As IDbDataParameter = cmd.CreateParameter
            //    prm.ParameterName = name
            //    prm.Value = value == null ? DBNull.Value: value;
            //    cmd.Parameters.Add(prm)
            //End Sub

            //'コマンドテキスト代入
            cmd.CommandText = Read(dsql, prefix, param, paramAdder);
            if (cmd.CommandText.Any() == false)
            {
                cmd.CommandText = dsql;
            }
            return cmd;
        }

        //''' <summary>
        //''' StaticSQLを作成します
        //''' </summary>
        //''' <param name="dsql"></param>
        //''' <param name="prefix">DynamicSQL</param>
        //''' <param name="params">ラメータ接頭語</param>
        //''' <param name="paramAdder">パラメータ値辞書</param>
        //''' <returns></returns>
        //''' <remarks></remarks>
        string Read(string dsql, string prefix, Dictionary<string, Object> param, Action<String, Object> paramAdder)
        {
            return ReadBlock(dsql, prefix, param, paramAdder);
        }

        //''' <summary>
        //''' ブロック読み込み
        //''' </summary>
        //''' <returns></returns>
        //''' <remarks>
        //''' 中身がない場合、コマンド（where、order by）自体を消す
        //''' </remarks>
        private static string ReadBlock(string dsql, string prefix, Dictionary<string, Object> param, Action<String, Object> paramAdder)
        {
            //'ブロック書式
            const string WHERE_BLOCK = "/\\*ds begin\\*/.*?(?<command>(where|order\\sby))(?<block>.*?)/\\*ds end begin\\*/[ ]?";

            StringBuilder s = new StringBuilder();
            int pos = 0;

            ///'dsqlを解析
            Match m = Regex.Match(dsql, WHERE_BLOCK, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (m.Success == false)
            {
                return ReadAsIfBlock(dsql, prefix, param, paramAdder);
            }

            while (m.Success)
            {
                Group g = m.Groups["command"];
                Group block = m.Groups["block"];
                string parts = ReadAsIfBlock(block.Value, prefix, param, paramAdder);

                //'コマンドテキスト
                if (string.IsNullOrEmpty(parts))
                {
                    //'ブロックが空の場合、コマンド句なしとする
                    s.AppendFormat("{0} ", dsql.Substring(pos, m.Index - pos));
                }
                else
                {
                    //'ブロックが空でない場合、コマンド句を記述する
                    s.AppendFormat("{0}{1}{2} ", dsql.Substring(pos, m.Index - pos), g.Value, parts.ToString());
                }

                //'事後処理
                pos = m.Index + m.Length;
                m = m.NextMatch();
            }

            return s.ToString().TrimEnd();
        }

        //''' <summary>
        //''' IFブロック読み込み
        //''' </summary>
        //''' <returns></returns>
        //''' <remarks>
        //''' NULLの場合条件式自体を消す
        //''' </remarks>
        private static string ReadAsIfBlock(string dsql, string prefix, Dictionary<string, Object> param, Action<String, Object> paramAdder)
        {
            //'書式
            const String IF_BLOCK = "/\\*ds if (?<name>.*?)[ ]?\\!\\=[ ]?null\\*/[ ]?\r\n(?<block>.*?)\r\n/\\*ds end if\\*/[ ]?";


            var s = new StringBuilder();
            int pos = 0;

            //'dsqlを解析
            var m = Regex.Match(dsql, IF_BLOCK, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (m.Success == false)
            {
                return ReadAsParamCode(dsql, prefix, param, paramAdder);
            }


            while (m.Success)
            {
                Group g = m.Groups["name"];
                Group block = m.Groups["block"];
                var val = param.Keys.Contains(g.Value) == false ? null : param[g.Value];
                string parts = string.Empty;

                if (!(val == null || DBNull.Value.Equals(val)))
                {
                    parts = ReadAsParamCode(block.Value, prefix, param, paramAdder);
                }

                //'コマンドテキスト
                if (string.IsNullOrEmpty(parts) == false)
                {
                    //'演算子（接頭書式）の処理
                    const string PREFIX_OPERATOR_CODE = "^(?<space>\\s*)(?<operator>(and|or)\\s*)(?<code>.*)";

                    var mPrefix = Regex.Match(parts, PREFIX_OPERATOR_CODE, RegexOptions.IgnoreCase);
                    var tmp = dsql.Substring(pos, m.Index - pos);

                    if (s.Length == 0 && string.IsNullOrEmpty(mPrefix.Groups["operator"].Value) == false)
                    {
                        s.AppendFormat("{0}{1}{2}", tmp, mPrefix.Groups["space"].Value, mPrefix.Groups["code"].Value);
                    }
                    else
                    {
                        s.AppendFormat("{0}{1}", tmp, parts.ToString());
                    }

                    //'事後処理
                    pos = m.Index + m.Length;
                    m = m.NextMatch();
                }
            }

            //'演算子（接尾書式）の処理
            const string SUFFIX_OPERATOR_CODE = "(?<code>.*)(?<op>(and|or))\\s*$";
            var opSuffix = Regex.Match(s.ToString(), SUFFIX_OPERATOR_CODE, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (opSuffix.Success)
            {
                return opSuffix.Groups["code"].Value.TrimEnd();
            }
            else
            {
                return s.ToString().TrimEnd();
            }

        }

        //''' <summary>
        //''' パラメータコード読み込み
        //''' </summary>
        //''' <returns></returns>
        //''' <remarks>
        //''' …/*ds 条件名*/ダミー値 …
        //''' <code>Age Between /*ds minage*/30 AND /*ds maxage*/40</code>
        //''' </remarks>
        private static string ReadAsParamCode(string dsql, string prefix, Dictionary<string, Object> param, Action<String, Object> paramAdder)
        {
            //'パラメータ書式
            const string PARAM_CODE = "/\\*ds (?<hard>\\$?)(?<name>[^ ]+)\\*/(?<dummy>[^ ]+)(?<space> ?)";

            var s = new StringBuilder();
            int pos = 0;

            //'dsqlを解析
            Match m = Regex.Match(dsql, PARAM_CODE, RegexOptions.IgnoreCase);

            if (m.Success == false)
            {
                return dsql;
            }

            Action increment = () =>
            {
                pos = m.Index + m.Length;
                m = m.NextMatch();
            };


            while (m.Success)
            {
                Group g = m.Groups["name"];
                Group sp = m.Groups["space"];

                if (param.ContainsKey(g.Value) == false)
                {
                    increment.Invoke();
                    continue;
                }

                var val = param[g.Value];
                var parts = string.Empty;

                if (string.IsNullOrEmpty(m.Groups["hard"].Value))
                {
                    //'$で始まらない場合、パラメータ
                    parts = CreateParameterCode(g.Value, val, prefix, paramAdder);
                }
                else
                {
                    //'$で始まる場合、埋め込み
                    parts = CreateHardCode(g.Value, val);
                }

                //'コマンドテキスト
                var pre = dsql.Substring(pos, m.Index - pos);
                if (Regex.IsMatch(pre, "\\sin\\s.*$", RegexOptions.IgnoreCase))
                {
                    //'IN句による括弧書き追加
                    parts = parts.Decorate("({0})");
                }
                s.AppendFormat("{0}{1}{2}", pre, parts, sp.Value);

                //'事後処理
                increment.Invoke();
            }

            //'残った文字列はそのまま付け足す
            s.Append(dsql.Substring(pos, dsql.Length - pos));

            return s.ToString().TrimEnd();
        }

        private static string CreateParameterCode(string name, Object val, string prefix, Action<String, Object> paramAdder)
        {
            ICollection vals = (ICollection)val;
            var parts = new StringBuilder();

            if (vals == null)
            {
                //'標準
                paramAdder.Invoke(name, val);
                parts.AppendFormat("{0}{1}", prefix, name);
            }
            else
            {
                //'配列
                int idx = 0;
                foreach (var item in vals)
                {
                    var s = string.Format("{0}_{1}", name, idx);
                    paramAdder.Invoke(s, item);
                    parts.AppendDelimiter(", ").AppendFormat("{0}{1}", prefix, s);
                    idx += 1;
                }
            }

            return parts.ToString();
        }

        private static string CreateHardCode(string name, Object val)
        {
            ICollection vals = (ICollection)val;
            var parts = new StringBuilder();

            if (vals == null)
            {
                //'標準
                parts.Append(val.ToString());
            }
            else
            {
                //'配列
                foreach (var item in vals)
                {
                    parts.AppendDelimiter(", ").Append(item.ToString());
                }
            }

            var s = parts.ToString();
            if (s.Contains(";"))
            {
                throw new ArgumentException(";を埋め込むことはできません。");
            }

            return s;
        }

    }

    //'拡張メソッドは別の名前空間を切っておき、既存コードへの影響を排除する
    namespace Extensions
    {
        //''' <summary>
        //''' StringBuilder拡張メソッド
        //''' </summary>
        //''' <remarks></remarks>
        static class StringBuilderExtension
        {

            //''' <summary>
            //''' 区切り文字を追加します。文字が存在しない場合は追加しません
            //''' </summary>
            //''' <param name="source"></param>
            //''' <param name="delimiter"></param>
            //''' <returns></returns>
            //''' <remarks></remarks>
            public static StringBuilder AppendDelimiter(this StringBuilder source, String delimiter)
            {
                if (source.Length != 0)
                {
                    source.Append(delimiter);
                }
                return source;
            }
        }

        //''' <summary>
        //''' String拡張メソッド
        //''' </summary>
        //''' <remarks></remarks>
        static class StringExtension
        {
            //''' <summary>
            //''' デコレートします
            //''' </summary>
            //''' <param name="source"></param>
            //''' <param name="format">{0}に現在値が入ります。</param>
            //''' <returns></returns>
            //''' <remarks><code>s.Decorate("({0})")</code></remarks>
            public static string Decorate(this string source, string format)
            {
                if (source.Length == 0)
                {
                    return source;
                }
                return string.Format(format, source);
            }
        }

        //''' <summary>
        //''' IDbCommand拡張メソッド
        //''' </summary>
        //''' <remarks></remarks>
        static class IDbCommandExtension
        {
            //''' <summary>
            //''' DBコマンドの情報を返します
            //''' </summary>
            //''' <param name="source"></param>
            //''' <returns></returns>
            //''' <remarks>デバッグ用のメソッド。</remarks>
            public static string ToInfoString(this IDbCommand source)
            {
                //'CommandText Info
                var s = new StringBuilder();
                s.Append(source.CommandText);
                if (source.Parameters.Count == 0)
                {
                    return s.ToString();
                }


                s.AppendLine();

                //'Parameter Info
                var prms = new StringBuilder();
                foreach (IDataParameter item in source.Parameters)
                {
                    prms.AppendDelimiter(", ").AppendFormat("{0}={1}", item.ParameterName, item.Value.ToString());
                }
                s.AppendFormat("--{0}", prms.ToString());
                
                return s.ToString();
            }
        }
    }
}
