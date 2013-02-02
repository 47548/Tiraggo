/*  New BSD License
-------------------------------------------------------------------------------
Copyright (c) 2006-2012, EntitySpaces, LLC
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:
    * Redistributions of source code must retain the above copyright
      notice, this list of conditions and the following disclaimer.
    * Redistributions in binary form must reproduce the above copyright
      notice, this list of conditions and the following disclaimer in the
      documentation and/or other materials provided with the distribution.
    * Neither the name of the EntitySpaces, LLC nor the
      names of its contributors may be used to endorse or promote products
      derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL EntitySpaces, LLC BE LIABLE FOR ANY
DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
-------------------------------------------------------------------------------
*/

using System;
using System.Collections.Generic;
using System.Data;
using Mono.Data.Sqlite;
using System.Diagnostics;
using System.Threading;

using Tiraggo.DynamicQuery;
using Tiraggo.Interfaces;

namespace Tiraggo.SQLiteProvider
{
    public class DataProvider : IDataProvider
    {
        public DataProvider()
        {
        }

        #region tgTraceArguments

        private sealed class tgTraceArguments : Tiraggo.Interfaces.ITraceArguments, IDisposable
        {
            static private long packetOrder = 0;

            private sealed class esTraceParameter : ITraceParameter
            {
                public string Name { get; set; }

                public string Direction { get; set; }

                public string ParamType { get; set; }

                public string BeforeValue { get; set; }

                public string AfterValue { get; set; }
            }

            public tgTraceArguments()
            {
            }

            public tgTraceArguments(tgDataRequest request, IDbCommand cmd, tgEntitySavePacket packet, string action, string callStack)
            {
                PacketOrder = Interlocked.Increment(ref tgTraceArguments.packetOrder);

                this.command = cmd;

                TraceChannel = DataProvider.sTraceChannel;
                Syntax = "SQLITE";
                Request = request;
                ThreadId = Thread.CurrentThread.ManagedThreadId;
                Action = action;
                CallStack = callStack;
                SqlCommand = cmd;
                ApplicationName = System.IO.Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location);

                IDataParameterCollection parameters = cmd.Parameters;

                if (parameters.Count > 0)
                {
                    Parameters = new List<ITraceParameter>(parameters.Count);

                    for (int i = 0; i < parameters.Count; i++)
                    {
                        SqliteParameter param = parameters[i] as SqliteParameter;

                        esTraceParameter p = new esTraceParameter()
                        {
                            Name = param.ParameterName,
                            Direction = param.Direction.ToString(),
                            ParamType = param.DbType.ToString().ToUpper(),
                            BeforeValue = param.Value != null && param.Value != DBNull.Value ? Convert.ToString(param.Value) : "null"
                        };

                        try
                        {
                            // Let's make it look like we're using parameters for the profiler
                            if (param.Value == null || param.Value == DBNull.Value)
                            {
                                if (param.SourceVersion == DataRowVersion.Current)
                                {
                                    object o = packet.CurrentValues[param.SourceColumn];
                                    if (o != null && o != DBNull.Value)
                                    {
                                        p.BeforeValue = Convert.ToString(o);
                                    }
                                }
                                else if (param.SourceVersion == DataRowVersion.Original)
                                {
                                    object o = packet.OriginalValues[param.SourceColumn];
                                    if (o != null && o != DBNull.Value)
                                    {
                                        p.BeforeValue = Convert.ToString(o);
                                    }
                                }
                            }
                        }
                        catch { }

                        this.Parameters.Add(p);
                    }
                }

                stopwatch = Stopwatch.StartNew();
            }

            public tgTraceArguments(tgDataRequest request, IDbCommand cmd, string action, string callStack)
            {
                PacketOrder = Interlocked.Increment(ref tgTraceArguments.packetOrder);

                this.command = cmd;

                TraceChannel = DataProvider.sTraceChannel;
                Syntax = "SQLITE";
                Request = request;
                ThreadId = Thread.CurrentThread.ManagedThreadId;
                Action = action;
                CallStack = callStack;
                SqlCommand = cmd;
                ApplicationName = System.IO.Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location);

                IDataParameterCollection parameters = cmd.Parameters;

                if (parameters.Count > 0)
                {
                    Parameters = new List<ITraceParameter>(parameters.Count);

                    for (int i = 0; i < parameters.Count; i++)
                    {
                        SqliteParameter param = parameters[i] as SqliteParameter;

                        esTraceParameter p = new esTraceParameter()
                        {
                            Name = param.ParameterName,
                            Direction = param.Direction.ToString(),
                            ParamType = param.DbType.ToString().ToUpper(),
                            BeforeValue = param.Value != null ? Convert.ToString(param.Value) : "null"
                        };

                        this.Parameters.Add(p);
                    }
                }

                stopwatch = Stopwatch.StartNew();
            }

            // Temporary variable
            private IDbCommand command;

            public long PacketOrder { get; set; }

            public string Syntax { get; set; }

            public tgDataRequest Request { get; set; }

            public int ThreadId { get; set; }

            public string Action { get; set; }

            public string CallStack { get; set; }

            public IDbCommand SqlCommand { get; set; }

            public string ApplicationName { get; set; }

            public string TraceChannel { get; set; }

            public long Duration { get; set; }

            public long Ticks { get; set; }

            public string Exception { get; set; }

            public List<ITraceParameter> Parameters { get; set; }

            private Stopwatch stopwatch;

            void IDisposable.Dispose()
            {
                stopwatch.Stop();
                Duration = stopwatch.ElapsedMilliseconds;
                Ticks = stopwatch.ElapsedTicks;

                // Gather Output Parameters
                if (this.Parameters != null && this.Parameters.Count > 0)
                {
                    IDataParameterCollection parameters = command.Parameters;

                    for (int i = 0; i < this.Parameters.Count; i++)
                    {
                        ITraceParameter esParam = this.Parameters[i];
                        IDbDataParameter param = parameters[esParam.Name] as IDbDataParameter;

                        if (param.Direction == ParameterDirection.InputOutput || param.Direction == ParameterDirection.Output)
                        {
                            esParam.AfterValue = param.Value != null ? Convert.ToString(param.Value) : "null";
                        }
                    }
                }

                DataProvider.sTraceHandler(this);
            }
        }

        #endregion tgTraceArguments

        #region Profiling Logic

        /// <summary>
        /// The EventHandler used to decouple the profiling code from the core assemblies
        /// </summary>
        event TraceEventHandler IDataProvider.TraceHandler
        {
            add { DataProvider.sTraceHandler += value; }
            remove { DataProvider.sTraceHandler -= value; }
        }

        static private event TraceEventHandler sTraceHandler;

        /// <summary>
        /// Returns true if this Provider is current being profiled
        /// </summary>
        bool IDataProvider.IsTracing
        {
            get
            {
                return sTraceHandler != null ? true : false;
            }
        }

        /// <summary>
        /// Used to set the Channel this provider is to use during Profiling
        /// </summary>
        string IDataProvider.TraceChannel
        {
            get { return DataProvider.sTraceChannel; }
            set { DataProvider.sTraceChannel = value; }
        }

        static private string sTraceChannel = "Channel1";

        #endregion Profiling Logic

        /// <summary>
        /// This method acts as a delegate for tgTransactionScope
        /// </summary>
        /// <returns></returns>
        static private IDbConnection CreateIDbConnectionDelegate()
        {
            return new SqliteConnection();
        }

        static private void CleanupCommand(SqliteCommand cmd)
        {
            if (cmd != null && cmd.Connection != null)
            {
                if (cmd.Connection.State == ConnectionState.Open)
                {
                    cmd.Connection.Close();
                }
            }
        }

        #region IDataProvider Members

        tgDataResponse IDataProvider.esLoadDataTable(tgDataRequest request)
        {
            tgDataResponse response = new tgDataResponse();

            try
            {
                switch (request.QueryType)
                {
                    case tgQueryType.StoredProcedure:

                        response = LoadDataTableFromStoredProcedure(request);
                        break;

                    case tgQueryType.Text:

                        response = LoadDataTableFromText(request);
                        break;

                    case tgQueryType.DynamicQuery:

                        response = new tgDataResponse();
                        SqliteCommand cmd = QueryBuilder.PrepareCommand(request);
                        LoadDataTableFromDynamicQuery(request, response, cmd);
                        break;

                    case tgQueryType.DynamicQueryParseOnly:

                        response = new tgDataResponse();
                        SqliteCommand cmd1 = QueryBuilder.PrepareCommand(request);
                        response.LastQuery = cmd1.CommandText;
                        break;

                    case tgQueryType.ManyToMany:

                        response = LoadManyToMany(request);
                        break;

                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                response.Exception = ex;
            }

            return response;
        }

        tgDataResponse IDataProvider.esSaveDataTable(tgDataRequest request)
        {
            tgDataResponse response = new tgDataResponse();

            try
            {
                if (request.SqlAccessType == tgSqlAccessType.StoredProcedure)
                {
                    if (request.CollectionSavePacket != null)
                        SaveStoredProcCollection(request);
                    else
                        SaveStoredProcEntity(request);
                }
                else
                {
                    if (request.EntitySavePacket.CurrentValues == null)
                        SaveDynamicCollection(request);
                    else
                        SaveDynamicEntity(request);
                }
            }
            catch (SqliteException ex)
            {
                tgException es = Shared.CheckForConcurrencyException(ex);
                if (es != null)
                    response.Exception = es;
                else
                    response.Exception = ex;
            }
            catch (DBConcurrencyException dbex)
            {
                response.Exception = new tgConcurrencyException("Error in SQLiteProvider.esSaveDataTable", dbex);
            }

            response.Table = request.Table;
            return response;
        }

        tgDataResponse IDataProvider.ExecuteNonQuery(tgDataRequest request)
        {
            tgDataResponse response = new tgDataResponse();
            SqliteCommand cmd = null;

            try
            {
                cmd = new SqliteCommand();
                if (request.CommandTimeout != null) cmd.CommandTimeout = request.CommandTimeout.Value;
                if (request.Parameters != null) Shared.AddParameters(cmd, request);

                switch (request.QueryType)
                {
                    case tgQueryType.TableDirect:
                        cmd.CommandType = CommandType.TableDirect;
                        cmd.CommandText = request.QueryText;
                        break;

                    case tgQueryType.StoredProcedure:
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.CommandText = Shared.CreateFullName(request);
                        break;

                    case tgQueryType.Text:
                        cmd.CommandType = CommandType.Text;
                        cmd.CommandText = request.QueryText;
                        break;
                }

                try
                {
                    tgTransactionScope.Enlist(cmd, request.ConnectionString, CreateIDbConnectionDelegate);

                    #region Profiling

                    if (sTraceHandler != null)
                    {
                        using (tgTraceArguments esTrace = new tgTraceArguments(request, cmd, "ExecuteNonQuery", System.Environment.StackTrace))
                        {
                            try
                            {
                                response.RowsEffected = cmd.ExecuteNonQuery();
                            }
                            catch (Exception ex)
                            {
                                esTrace.Exception = ex.Message;
                                throw;
                            }
                        }
                    }
                    else

                    #endregion Profiling

                    {
                        response.RowsEffected = cmd.ExecuteNonQuery();
                    }
                }
                finally
                {
                    tgTransactionScope.DeEnlist(cmd);
                }

                if (request.Parameters != null)
                {
                    Shared.GatherReturnParameters(cmd, request, response);
                }
            }
            catch (Exception ex)
            {
                CleanupCommand(cmd);
                response.Exception = ex;
            }

            return response;
        }

        tgDataResponse IDataProvider.ExecuteReader(tgDataRequest request)
        {
            tgDataResponse response = new tgDataResponse();
            SqliteCommand cmd = null;

            try
            {
                cmd = new SqliteCommand();
                if (request.CommandTimeout != null) cmd.CommandTimeout = request.CommandTimeout.Value;
                if (request.Parameters != null) Shared.AddParameters(cmd, request);

                switch (request.QueryType)
                {
                    case tgQueryType.TableDirect:
                        cmd.CommandType = CommandType.TableDirect;
                        cmd.CommandText = request.QueryText;
                        break;

                    case tgQueryType.StoredProcedure:
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.CommandText = Shared.CreateFullName(request);
                        break;

                    case tgQueryType.Text:
                        cmd.CommandType = CommandType.Text;
                        cmd.CommandText = request.QueryText;
                        break;

                    case tgQueryType.DynamicQuery:
                        cmd = QueryBuilder.PrepareCommand(request);
                        break;
                }

                cmd.Connection = new SqliteConnection(request.ConnectionString);
                cmd.Connection.Open();

                #region Profiling

                if (sTraceHandler != null)
                {
                    using (tgTraceArguments esTrace = new tgTraceArguments(request, cmd, "ExecuteReader", System.Environment.StackTrace))
                    {
                        try
                        {
                            response.DataReader = cmd.ExecuteReader(CommandBehavior.CloseConnection);
                        }
                        catch (Exception ex)
                        {
                            esTrace.Exception = ex.Message;
                            throw;
                        }
                    }
                }
                else

                #endregion Profiling

                {
                    response.DataReader = cmd.ExecuteReader(CommandBehavior.CloseConnection);
                }
            }
            catch (Exception ex)
            {
                CleanupCommand(cmd);
                response.Exception = ex;
            }

            return response;
        }

        tgDataResponse IDataProvider.ExecuteScalar(tgDataRequest request)
        {
            tgDataResponse response = new tgDataResponse();
            SqliteCommand cmd = null;

            try
            {
                cmd = new SqliteCommand();
                if (request.CommandTimeout != null) cmd.CommandTimeout = request.CommandTimeout.Value;
                if (request.Parameters != null) Shared.AddParameters(cmd, request);

                switch (request.QueryType)
                {
                    case tgQueryType.TableDirect:
                        cmd.CommandType = CommandType.TableDirect;
                        cmd.CommandText = request.QueryText;
                        break;

                    case tgQueryType.StoredProcedure:
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.CommandText = Shared.CreateFullName(request);
                        break;

                    case tgQueryType.Text:
                        cmd.CommandType = CommandType.Text;
                        cmd.CommandText = request.QueryText;
                        break;

                    case tgQueryType.DynamicQuery:
                        cmd = QueryBuilder.PrepareCommand(request);
                        break;
                }

                try
                {
                    tgTransactionScope.Enlist(cmd, request.ConnectionString, CreateIDbConnectionDelegate);

                    #region Profiling

                    if (sTraceHandler != null)
                    {
                        using (tgTraceArguments esTrace = new tgTraceArguments(request, cmd, "ExecuteScalar", System.Environment.StackTrace))
                        {
                            try
                            {
                                response.Scalar = cmd.ExecuteScalar();
                            }
                            catch (Exception ex)
                            {
                                esTrace.Exception = ex.Message;
                                throw;
                            }
                        }
                    }
                    else

                    #endregion Profiling

                    {
                        response.Scalar = cmd.ExecuteScalar();
                    }
                }
                finally
                {
                    tgTransactionScope.DeEnlist(cmd);
                }

                if (request.Parameters != null)
                {
                    Shared.GatherReturnParameters(cmd, request, response);
                }
            }
            catch (Exception ex)
            {
                CleanupCommand(cmd);
                response.Exception = ex;
            }

            return response;
        }

        tgDataResponse IDataProvider.FillDataSet(tgDataRequest request)
        {
            tgDataResponse response = new tgDataResponse();

            try
            {
                switch (request.QueryType)
                {
                    case tgQueryType.StoredProcedure:

                        response = LoadDataSetFromStoredProcedure(request);
                        break;

                    case tgQueryType.Text:

                        response = LoadDataSetFromText(request);
                        break;

                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                response.Exception = ex;
            }

            return response;
        }

        tgDataResponse IDataProvider.FillDataTable(tgDataRequest request)
        {
            tgDataResponse response = new tgDataResponse();

            try
            {
                switch (request.QueryType)
                {
                    case tgQueryType.StoredProcedure:

                        response = LoadDataTableFromStoredProcedure(request);
                        break;

                    case tgQueryType.Text:

                        response = LoadDataTableFromText(request);
                        break;

                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                response.Exception = ex;
            }

            return response;
        }

        #endregion IDataProvider Members

        static private tgDataResponse LoadDataSetFromStoredProcedure(tgDataRequest request)
        {
            tgDataResponse response = new tgDataResponse();
            SqliteCommand cmd = null;

            try
            {
                DataSet dataSet = new DataSet();

                cmd = new SqliteCommand();
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = Shared.CreateFullName(request);

                if (request.CommandTimeout != null) cmd.CommandTimeout = request.CommandTimeout.Value;
                if (request.Parameters != null) Shared.AddParameters(cmd, request);

                SqliteDataAdapter da = new SqliteDataAdapter();
                da.SelectCommand = cmd;

                try
                {
                    tgTransactionScope.Enlist(da.SelectCommand, request.ConnectionString, CreateIDbConnectionDelegate);

                    #region Profiling

                    if (sTraceHandler != null)
                    {
                        using (tgTraceArguments esTrace = new tgTraceArguments(request, cmd, "LoadFromStoredProcedure", System.Environment.StackTrace))
                        {
                            try
                            {
                                da.Fill(dataSet);
                            }
                            catch (Exception ex)
                            {
                                esTrace.Exception = ex.Message;
                                throw;
                            }
                        }
                    }
                    else

                    #endregion Profiling

                    {
                        da.Fill(dataSet);
                    }
                }
                finally
                {
                    tgTransactionScope.DeEnlist(da.SelectCommand);
                }

                response.DataSet = dataSet;

                if (request.Parameters != null)
                {
                    Shared.GatherReturnParameters(cmd, request, response);
                }
            }
            catch (Exception ex)
            {
                CleanupCommand(cmd);
                throw ex;
            }
            finally
            {
            }

            return response;
        }

        static private tgDataResponse LoadDataSetFromText(tgDataRequest request)
        {
            tgDataResponse response = new tgDataResponse();
            SqliteCommand cmd = null;

            try
            {
                DataSet dataSet = new DataSet();

                cmd = new SqliteCommand();
                if (request.CommandTimeout != null) cmd.CommandTimeout = request.CommandTimeout.Value;
                if (request.Parameters != null) Shared.AddParameters(cmd, request);

                SqliteDataAdapter da = new SqliteDataAdapter();
                cmd.CommandText = request.QueryText;
                da.SelectCommand = cmd;

                try
                {
                    tgTransactionScope.Enlist(da.SelectCommand, request.ConnectionString, CreateIDbConnectionDelegate);

                    #region Profiling

                    if (sTraceHandler != null)
                    {
                        using (tgTraceArguments esTrace = new tgTraceArguments(request, cmd, "LoadDataSetFromText", System.Environment.StackTrace))
                        {
                            try
                            {
                                da.Fill(dataSet);
                            }
                            catch (Exception ex)
                            {
                                esTrace.Exception = ex.Message;
                                throw;
                            }
                        }
                    }
                    else

                    #endregion Profiling

                    {
                        da.Fill(dataSet);
                    }
                }
                finally
                {
                    tgTransactionScope.DeEnlist(da.SelectCommand);
                }

                response.DataSet = dataSet;

                if (request.Parameters != null)
                {
                    Shared.GatherReturnParameters(cmd, request, response);
                }
            }
            catch (Exception ex)
            {
                CleanupCommand(cmd);
                throw ex;
            }
            finally
            {
            }

            return response;
        }

        static private tgDataResponse LoadDataTableFromStoredProcedure(tgDataRequest request)
        {
            tgDataResponse response = new tgDataResponse();
            SqliteCommand cmd = null;

            try
            {
                DataTable dataTable = new DataTable(request.ProviderMetadata.Destination);

                cmd = new SqliteCommand();
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = Shared.CreateFullName(request);
                if (request.CommandTimeout != null) cmd.CommandTimeout = request.CommandTimeout.Value;
                if (request.Parameters != null) Shared.AddParameters(cmd, request);

                SqliteDataAdapter da = new SqliteDataAdapter();
                da.SelectCommand = cmd;

                try
                {
                    tgTransactionScope.Enlist(da.SelectCommand, request.ConnectionString, CreateIDbConnectionDelegate);

                    #region Profiling

                    if (sTraceHandler != null)
                    {
                        using (tgTraceArguments esTrace = new tgTraceArguments(request, cmd, "LoadFromStoredProcedure", System.Environment.StackTrace))
                        {
                            try
                            {
                                da.Fill(dataTable);
                            }
                            catch (Exception ex)
                            {
                                esTrace.Exception = ex.Message;
                                throw;
                            }
                        }
                    }
                    else

                    #endregion Profiling

                    {
                        da.Fill(dataTable);
                    }
                }
                finally
                {
                    tgTransactionScope.DeEnlist(da.SelectCommand);
                }

                response.Table = dataTable;

                if (request.Parameters != null)
                {
                    Shared.GatherReturnParameters(cmd, request, response);
                }
            }
            catch (Exception ex)
            {
                CleanupCommand(cmd);
                throw ex;
            }
            finally
            {
            }

            return response;
        }

        static private tgDataResponse LoadDataTableFromText(tgDataRequest request)
        {
            tgDataResponse response = new tgDataResponse();
            SqliteCommand cmd = null;

            try
            {
                DataTable dataTable = new DataTable(request.ProviderMetadata.Destination);

                cmd = new SqliteCommand();
                cmd.CommandType = CommandType.Text;
                if (request.CommandTimeout != null) cmd.CommandTimeout = request.CommandTimeout.Value;
                if (request.Parameters != null) Shared.AddParameters(cmd, request);

                SqliteDataAdapter da = new SqliteDataAdapter();
                cmd.CommandText = request.QueryText;
                da.SelectCommand = cmd;

                try
                {
                    tgTransactionScope.Enlist(da.SelectCommand, request.ConnectionString, CreateIDbConnectionDelegate);

                    #region Profiling

                    if (sTraceHandler != null)
                    {
                        using (tgTraceArguments esTrace = new tgTraceArguments(request, cmd, "LoadFromText", System.Environment.StackTrace))
                        {
                            try
                            {
                                da.Fill(dataTable);
                            }
                            catch (Exception ex)
                            {
                                esTrace.Exception = ex.Message;
                                throw;
                            }
                        }
                    }
                    else

                    #endregion Profiling

                    {
                        da.Fill(dataTable);
                    }
                }
                finally
                {
                    tgTransactionScope.DeEnlist(da.SelectCommand);
                }

                response.Table = dataTable;

                if (request.Parameters != null)
                {
                    Shared.GatherReturnParameters(cmd, request, response);
                }
            }
            catch (Exception ex)
            {
                CleanupCommand(cmd);
                throw ex;
            }
            finally
            {
            }

            return response;
        }

        static private tgDataResponse LoadManyToMany(tgDataRequest request)
        {
            tgDataResponse response = new tgDataResponse();
            SqliteCommand cmd = null;

            try
            {
                DataTable dataTable = new DataTable(request.ProviderMetadata.Destination);

                cmd = new SqliteCommand();
                cmd.CommandType = CommandType.Text;
                if (request.CommandTimeout != null) cmd.CommandTimeout = request.CommandTimeout.Value;

                string mmQuery = request.QueryText;

                string[] sections = mmQuery.Split('|');
                string[] tables = sections[0].Split(',');
                string[] columns = sections[1].Split(',');

                // We build the query, we don't use Delimiters to avoid tons of extra concatentation
                string sql = "SELECT * FROM [" + tables[0];
                sql += "] JOIN [" + tables[1] + "] ON [" + tables[0] + "].[" + columns[0] + "] = [";
                sql += tables[1] + "].[" + columns[1];
                sql += "] WHERE [" + tables[1] + "].[" + sections[2] + "] = @";

                if (request.Parameters != null)
                {
                    foreach (tgParameter esParam in request.Parameters)
                    {
                        sql += esParam.Name;
                    }

                    Shared.AddParameters(cmd, request);
                }

                SqliteDataAdapter da = new SqliteDataAdapter();
                cmd.CommandText = sql;

                da.SelectCommand = cmd;

                try
                {
                    tgTransactionScope.Enlist(da.SelectCommand, request.ConnectionString, CreateIDbConnectionDelegate);

                    #region Profiling

                    if (sTraceHandler != null)
                    {
                        using (tgTraceArguments esTrace = new tgTraceArguments(request, cmd, "LoadManyToMany", System.Environment.StackTrace))
                        {
                            try
                            {
                                da.Fill(dataTable);
                            }
                            catch (Exception ex)
                            {
                                esTrace.Exception = ex.Message;
                                throw;
                            }
                        }
                    }
                    else

                    #endregion Profiling

                    {
                        da.Fill(dataTable);
                    }
                }
                finally
                {
                    tgTransactionScope.DeEnlist(da.SelectCommand);
                }

                response.Table = dataTable;
            }
            catch (Exception ex)
            {
                CleanupCommand(cmd);
                throw ex;
            }
            finally
            {
            }

            return response;
        }

        // This is used only to execute the Dynamic Query API
        static private void LoadDataTableFromDynamicQuery(tgDataRequest request, tgDataResponse response, SqliteCommand cmd)
        {
            try
            {
                response.LastQuery = cmd.CommandText;

                if (request.CommandTimeout != null) cmd.CommandTimeout = request.CommandTimeout.Value;

                DataTable dataTable = new DataTable(request.ProviderMetadata.Destination);

                SqliteDataAdapter da = new SqliteDataAdapter();
                da.SelectCommand = cmd;

                try
                {
                    tgTransactionScope.Enlist(da.SelectCommand, request.ConnectionString, CreateIDbConnectionDelegate);

                    #region Profiling

                    if (sTraceHandler != null)
                    {
                        using (tgTraceArguments esTrace = new tgTraceArguments(request, cmd, "LoadFromDynamicQuery", System.Environment.StackTrace))
                        {
                            try
                            {
                                da.Fill(dataTable);
                            }
                            catch (Exception ex)
                            {
                                esTrace.Exception = ex.Message;
                                throw;
                            }
                        }
                    }
                    else

                    #endregion Profiling

                    {
                        da.Fill(dataTable);
                    }
                }
                finally
                {
                    tgTransactionScope.DeEnlist(da.SelectCommand);
                }

                response.Table = dataTable;
            }
            catch (Exception ex)
            {
                CleanupCommand(cmd);
                throw ex;
            }
            finally
            {
            }
        }

        static private DataTable SaveStoredProcCollection(tgDataRequest request)
        {
            throw new NotImplementedException("Stored Procedure Support Not Implemented");
        }

        static private DataTable SaveStoredProcEntity(tgDataRequest request)
        {
            throw new NotImplementedException("Stored Procedure Support Not Implemented");
        }

        static private DataTable SaveDynamicCollection(tgDataRequest request)
        {
            tgEntitySavePacket pkt = request.CollectionSavePacket[0];

            if (pkt.RowState == tgDataRowState.Deleted)
            {
                //============================================================================
                // We do all our deletes at once, so if the first one is a delete they all are
                //============================================================================
                return SaveDynamicCollection_Deletes(request);
            }
            else
            {
                //============================================================================
                // We do all our Inserts and Updates at once
                //============================================================================
                return SaveDynamicCollection_InsertsUpdates(request);
            }
        }

        static private DataTable SaveDynamicCollection_InsertsUpdates(tgDataRequest request)
        {
            DataTable dataTable = CreateDataTable(request);

            using (tgTransactionScope scope = new tgTransactionScope())
            {
                using (SqliteDataAdapter da = new SqliteDataAdapter())
                {
                    da.AcceptChangesDuringUpdate = false;
                    da.ContinueUpdateOnError = request.ContinueUpdateOnError;

                    SqliteCommand cmd = null;

                    if (!request.IgnoreComputedColumns)
                    {
                        da.RowUpdated += new EventHandler<System.Data.Common.RowUpdatedEventArgs>(OnRowUpdated);
                    }

                    foreach (tgEntitySavePacket packet in request.CollectionSavePacket)
                    {
                        if (packet.RowState != tgDataRowState.Added && packet.RowState != tgDataRowState.Modified) continue;

                        DataRow row = dataTable.NewRow();
                        dataTable.Rows.Add(row);

                        switch (packet.RowState)
                        {
                            case tgDataRowState.Added:
                                cmd = da.InsertCommand = Shared.BuildDynamicInsertCommand(request, packet);
                                SetModifiedValues(request, packet, row);
                                break;

                            case tgDataRowState.Modified:
                                cmd = da.UpdateCommand = Shared.BuildDynamicUpdateCommand(request, packet);
                                SetOriginalValues(request, packet, row, false);
                                SetModifiedValues(request, packet, row);
                                row.AcceptChanges();
                                row.SetModified();
                                break;
                        }

                        request.Properties["tgDataRequest"] = request;
                        request.Properties["esEntityData"] = packet;
                        dataTable.ExtendedProperties["props"] = request.Properties;

                        DataRow[] singleRow = new DataRow[1];
                        singleRow[0] = row;

                        try
                        {
                            tgTransactionScope.Enlist(cmd, request.ConnectionString, CreateIDbConnectionDelegate);

                            #region Profiling

                            if (sTraceHandler != null)
                            {
                                using (tgTraceArguments esTrace = new tgTraceArguments(request, cmd, packet, "SaveCollectionDynamic", System.Environment.StackTrace))
                                {
                                    try
                                    {
                                        da.Update(singleRow);
                                    }
                                    catch (Exception ex)
                                    {
                                        esTrace.Exception = ex.Message;
                                        throw;
                                    }
                                }
                            }
                            else

                            #endregion Profiling

                            {
                                da.Update(singleRow);
                            }

                            if (row.HasErrors)
                            {
                                request.FireOnError(packet, row.RowError);
                            }
                        }
                        finally
                        {
                            tgTransactionScope.DeEnlist(cmd);
                            dataTable.Rows.Clear();
                        }

                        if (!row.HasErrors && packet.RowState != tgDataRowState.Deleted && cmd.Parameters != null)
                        {
                            foreach (SqliteParameter param in cmd.Parameters)
                            {
                                switch (param.Direction)
                                {
                                    case ParameterDirection.Output:
                                    case ParameterDirection.InputOutput:

                                        packet.CurrentValues[param.SourceColumn] = param.Value;
                                        break;
                                }
                            }
                        }

                        cmd.Dispose();
                    }
                }

                scope.Complete();
            }

            return dataTable;
        }

        static private DataTable SaveDynamicCollection_Deletes(tgDataRequest request)
        {
            SqliteCommand cmd = null;

            DataTable dataTable = CreateDataTable(request);

            using (tgTransactionScope scope = new tgTransactionScope())
            {
                using (SqliteDataAdapter da = new SqliteDataAdapter())
                {
                    da.AcceptChangesDuringUpdate = false;
                    da.ContinueUpdateOnError = request.ContinueUpdateOnError;

                    try
                    {
                        cmd = da.DeleteCommand = Shared.BuildDynamicDeleteCommand(request);
                        tgTransactionScope.Enlist(cmd, request.ConnectionString, CreateIDbConnectionDelegate);

                        DataRow[] singleRow = new DataRow[1];

                        // Delete each record
                        foreach (tgEntitySavePacket packet in request.CollectionSavePacket)
                        {
                            DataRow row = dataTable.NewRow();
                            dataTable.Rows.Add(row);

                            SetOriginalValues(request, packet, row, true);
                            row.AcceptChanges();
                            row.Delete();

                            singleRow[0] = row;

                            #region Profiling

                            if (sTraceHandler != null)
                            {
                                using (tgTraceArguments esTrace = new tgTraceArguments(request, cmd, packet, "SaveCollectionDynamic", System.Environment.StackTrace))
                                {
                                    try
                                    {
                                        da.Update(singleRow);
                                    }
                                    catch (Exception ex)
                                    {
                                        esTrace.Exception = ex.Message;
                                        throw;
                                    }
                                }
                            }
                            else

                            #endregion Profiling

                            {
                                da.Update(singleRow);
                            }

                            if (row.HasErrors)
                            {
                                request.FireOnError(packet, row.RowError);
                            }

                            dataTable.Rows.Clear(); // ADO.NET won't let us reuse the same DataRow
                        }
                    }
                    finally
                    {
                        tgTransactionScope.DeEnlist(cmd);
                        cmd.Dispose();
                    }
                }
                scope.Complete();
            }

            return request.Table;
        }

        static private DataTable SaveDynamicEntity(tgDataRequest request)
        {
            bool needToDelete = request.EntitySavePacket.RowState == tgDataRowState.Deleted;

            DataTable dataTable = CreateDataTable(request);

            using (SqliteDataAdapter da = new SqliteDataAdapter())
            {
                da.AcceptChangesDuringUpdate = false;

                DataRow row = dataTable.NewRow();
                dataTable.Rows.Add(row);

                SqliteCommand cmd = null;

                switch (request.EntitySavePacket.RowState)
                {
                    case tgDataRowState.Added:
                        cmd = da.InsertCommand = Shared.BuildDynamicInsertCommand(request, request.EntitySavePacket);
                        SetModifiedValues(request, request.EntitySavePacket, row);
                        break;

                    case tgDataRowState.Modified:
                        cmd = da.UpdateCommand = Shared.BuildDynamicUpdateCommand(request, request.EntitySavePacket);
                        SetOriginalValues(request, request.EntitySavePacket, row, false);
                        SetModifiedValues(request, request.EntitySavePacket, row);
                        row.AcceptChanges();
                        row.SetModified();
                        break;

                    case tgDataRowState.Deleted:
                        cmd = da.DeleteCommand = Shared.BuildDynamicDeleteCommand(request);
                        SetOriginalValues(request, request.EntitySavePacket, row, true);
                        row.AcceptChanges();
                        row.Delete();
                        break;
                }

                if (!needToDelete && request.Properties != null)
                {
                    request.Properties["tgDataRequest"] = request;
                    request.Properties["esEntityData"] = request.EntitySavePacket;
                    dataTable.ExtendedProperties["props"] = request.Properties;
                }

                DataRow[] singleRow = new DataRow[1];
                singleRow[0] = row;

                try
                {
                    if (!request.IgnoreComputedColumns)
                    {
                        da.RowUpdated += new EventHandler<System.Data.Common.RowUpdatedEventArgs>(OnRowUpdated);
                    }
                    tgTransactionScope.Enlist(cmd, request.ConnectionString, CreateIDbConnectionDelegate);

                    #region Profiling

                    if (sTraceHandler != null)
                    {
                        using (tgTraceArguments esTrace = new tgTraceArguments(request, cmd, request.EntitySavePacket, "SaveEntityDynamic", System.Environment.StackTrace))
                        {
                            try
                            {
                                da.Update(singleRow);
                            }
                            catch (Exception ex)
                            {
                                esTrace.Exception = ex.Message;
                                throw;
                            }
                        }
                    }
                    else

                    #endregion Profiling

                    {
                        da.Update(singleRow);
                    }
                }
                finally
                {
                    tgTransactionScope.DeEnlist(cmd);
                }

                if (request.EntitySavePacket.RowState != tgDataRowState.Deleted && cmd.Parameters != null)
                {
                    foreach (SqliteParameter param in cmd.Parameters)
                    {
                        switch (param.Direction)
                        {
                            case ParameterDirection.Output:
                            case ParameterDirection.InputOutput:

                                request.EntitySavePacket.CurrentValues[param.SourceColumn] = param.Value;
                                break;
                        }
                    }
                }

                cmd.Dispose();
            }

            return dataTable;
        }

        static private DataTable CreateDataTable(tgDataRequest request)
        {
            DataTable dataTable = new DataTable();
            DataColumnCollection dataColumns = dataTable.Columns;
            tgColumnMetadataCollection cols = request.Columns;

            if (request.SelectedColumns == null)
            {
                tgColumnMetadata col;
                for (int i = 0; i < cols.Count; i++)
                {
                    col = cols[i];
                    dataColumns.Add(new DataColumn(col.Name, col.Type));
                }
            }
            else
            {
                foreach (string col in request.SelectedColumns.Keys)
                {
                    dataColumns.Add(new DataColumn(col, cols[col].Type));
                }
            }

            return dataTable;
        }

        private static void SetOriginalValues(tgDataRequest request, tgEntitySavePacket packet, DataRow row, bool primaryKeysAndConcurrencyOnly)
        {
            foreach (tgColumnMetadata col in request.Columns)
            {
                if (primaryKeysAndConcurrencyOnly &&
                    (!col.IsInPrimaryKey && !col.IsConcurrency && !col.IsTiraggoConcurrency)) continue;

                string columnName = col.Name;

                if (packet.OriginalValues.ContainsKey(columnName))
                {
                    row[columnName] = packet.OriginalValues[columnName];
                }
            }
        }

        private static void SetModifiedValues(tgDataRequest request, tgEntitySavePacket packet, DataRow row)
        {
            foreach (string column in packet.ModifiedColumns)
            {
                if (request.Columns.FindByColumnName(column) != null)
                {
                    row[column] = packet.CurrentValues[column];
                }
            }
        }

        // If it's an Insert we fetch the @@Identity value and stuff it in the proper column
        protected static void OnRowUpdated(object sender, System.Data.Common.RowUpdatedEventArgs e)
        {
            try
            {
                PropertyCollection props = e.Row.Table.ExtendedProperties;
                if (props.ContainsKey("props"))
                {
                    props = (PropertyCollection)props["props"];
                }

                if (e.Status == UpdateStatus.Continue && (e.StatementType == StatementType.Insert || e.StatementType == StatementType.Update))
                {
                    tgDataRequest request = props["tgDataRequest"] as tgDataRequest;
                    tgEntitySavePacket packet = (tgEntitySavePacket)props["esEntityData"];

                    if (e.StatementType == StatementType.Insert)
                    {
                        if (props.Contains("AutoInc"))
                        {
                            string autoInc = props["AutoInc"] as string;

                            SqliteCommand cmd = new SqliteCommand();
                            cmd.Connection = e.Command.Connection as SqliteConnection;
                            cmd.Transaction = e.Command.Transaction as SqliteTransaction;

                            cmd.CommandText = "SELECT last_insert_rowid()";
                            cmd.CommandType = CommandType.Text;

                            object o = null;

                            #region Profiling

                            if (sTraceHandler != null)
                            {
                                using (tgTraceArguments esTrace = new tgTraceArguments(request, cmd, "OnRowUpdated", System.Environment.StackTrace))
                                {
                                    try
                                    {
                                        o = cmd.ExecuteScalar();
                                    }
                                    catch (Exception ex)
                                    {
                                        esTrace.Exception = ex.Message;
                                        throw;
                                    }
                                }
                            }
                            else

                            #endregion Profiling

                            {
                                o = cmd.ExecuteScalar();
                            }

                            if (o != null)
                            {
                                packet.CurrentValues[autoInc] = o;
                                e.Row[autoInc] = o;
                            }
                        }

                        if (props.Contains("EntitySpacesConcurrency"))
                        {
                            string esConcurrencyColumn = props["EntitySpacesConcurrency"] as string;
                            packet.CurrentValues[esConcurrencyColumn] = 1;
                        }
                    }

                    //-------------------------------------------------------------------------------------------------
                    // Fetch any defaults, SQLite doesn't support output parameters so we gotta do this the hard way
                    //-------------------------------------------------------------------------------------------------
                    if (props.Contains("Defaults"))
                    {
                        // Build the Where parameter and parameters
                        SqliteCommand cmd = new SqliteCommand();
                        cmd.Connection = e.Command.Connection as SqliteConnection;
                        cmd.Transaction = e.Command.Transaction as SqliteTransaction;

                        string select = (string)props["Defaults"];

                        string[] whereParameters = ((string)props["Where"]).Split(',');

                        string comma = String.Empty;
                        string where = String.Empty;
                        int i = 1;
                        foreach (string parameter in whereParameters)
                        {
                            SqliteParameter p = new SqliteParameter("@p" + i++.ToString(), e.Row[parameter]);
                            cmd.Parameters.Add(p);
                            where += comma + "[" + parameter + "]=" + p.ParameterName;
                            comma = " AND ";
                        }

                        // Okay, now we can execute the sql and get any values that have defaults that were
                        // null at the time of the insert and/or our timestamp
                        cmd.CommandText = "SELECT " + select + " FROM [" + request.ProviderMetadata.Source + "] WHERE " + where + ";";

                        SqliteDataReader rdr = null;

                        try
                        {
                            #region Profiling

                            if (sTraceHandler != null)
                            {
                                using (tgTraceArguments esTrace = new tgTraceArguments(request, cmd, "OnRowUpdated", System.Environment.StackTrace))
                                {
                                    try
                                    {
                                        rdr = cmd.ExecuteReader(CommandBehavior.SingleResult);
                                    }
                                    catch (Exception ex)
                                    {
                                        esTrace.Exception = ex.Message;
                                        throw;
                                    }
                                }
                            }
                            else

                            #endregion Profiling

                            {
                                rdr = cmd.ExecuteReader(CommandBehavior.SingleResult);
                            }

                            if (rdr.Read())
                            {
                                select = select.Replace("[", String.Empty).Replace("]", String.Empty);
                                string[] selectCols = select.Split(',');

                                for (int k = 0; k < selectCols.Length; k++)
                                {
                                    packet.CurrentValues[selectCols[k]] = rdr.GetValue(k);
                                }
                            }
                        }
                        finally
                        {
                            // Make sure we close the reader no matter what
                            if (rdr != null) rdr.Close();
                        }
                    }

                    if (e.StatementType == StatementType.Update)
                    {
                        string colName = props["EntitySpacesConcurrency"] as string;
                        object o = e.Row[colName];

                        switch (Type.GetTypeCode(o.GetType()))
                        {
                            case TypeCode.Int16: packet.CurrentValues[colName] = ((System.Int16)o) + 1; break;
                            case TypeCode.Int32: packet.CurrentValues[colName] = ((System.Int32)o) + 1; break;
                            case TypeCode.Int64: packet.CurrentValues[colName] = ((System.Int64)o) + 1; break;
                            case TypeCode.UInt16: packet.CurrentValues[colName] = ((System.UInt16)o) + 1; break;
                            case TypeCode.UInt32: packet.CurrentValues[colName] = ((System.UInt32)o) + 1; break;
                            case TypeCode.UInt64: packet.CurrentValues[colName] = ((System.UInt64)o) + 1; break;
                        }
                    }
                }
            }
            catch { }
        }
    }
}