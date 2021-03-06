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
using System.Data;
using System.Collections.Generic;
using System.Runtime.Serialization;

using Tiraggo.DynamicQuery;
using System.ComponentModel;

namespace Tiraggo.Interfaces
{
    /// <summary>
    /// Used internally by EntitySpaces in support of the DynamicQuery Prefetch logic. This is passed to each
    /// esPrefetchDelegate delegate 
    /// </summary>
    public class tgPrefetchParameters
    {
        /// <summary>
        /// The Root query when the Prefetch path is complete
        /// </summary>
        public tgDynamicQuery Root;

        /// <summary>
        /// In the esPrefetchDelegate if this is non-null this is "you" so don't create your Query, it was created in the 
        /// esPrefetchDelegate just before you were called
        /// </summary>
        public tgDynamicQuery You;

        /// <summary>
        /// Used by the prefetch logic, alias = "a" + Alias++.ToString()
        /// </summary>
        public int Alias = 1;

        public string NextAlias()
        {
            return "a" + Alias++.ToString();
        }
    }

    /// <summary>
    /// Used internally by EntitySpaces in support of the DynamicQuery Prefetch logic
    /// </summary>
    public class tgPrefetchMap
    {
        /// <summary>
        /// The Query for the Prefetch, only on the root tgPrefetchMap will contain a query
        /// </summary>
        public tgDynamicQuery Query;
        /// <summary>
        /// The Query for the Prefetch, only on the root tgPrefetchMap will contain a DataTable (after the call to Query.Load())
        /// </summary>
        public DataTable Table;
        /// <summary>
        /// The Prefetch delegate itself. This is used when building the query
        /// </summary>
        public tgPrefetchDelegate PrefetchDelegate;
        /// <summary>
        /// The Property name this prefetch Property, ie, "OrdersCollectionByEmployeeID"
        /// </summary>
        public string PropertyName;
        /// <summary>
        /// The name of the column in this entity
        /// </summary>
        public string MyColumnName;
        /// <summary>
        /// The name of the column in the parent entity
        /// </summary>
        public string ParentColumnName;
        /// <summary>
        /// If this is a SubPath 
        /// </summary>
        public string Path;
        /// <summary>
        /// True if the foreignkey is a composite key
        /// </summary>
        public bool IsMultiPartKey;
    }

    /// <summary>
    /// A Prefetch delegate map entry used when building a Prefetch path
    /// </summary>
    /// <param name="data">Used to pass state to each esPrefetchDelegate delegate as the query is being created</param>
    public delegate void tgPrefetchDelegate(tgPrefetchParameters data);

    /// <summary>
    /// This provides the Dynamic Query mechanism used by your Business object (Employees),
    /// collection (EmployeesCollection), and query caching (EmployeesQuery).
    /// </summary>
    /// <example>
    /// DynamicQuery allows you to (without writing any stored procedures)
    /// query your database on the fly. All selection criteria are passed in
    /// via Parameters (SAParameter, OleDbParameter) in order to prevent
    /// sql injection techniques often attempted by hackers.  
    /// Additional examples are provided here:
    /// <code>
    /// http://www.entityspaces.net/portal/QueryAPISamples/tabid/80/Default.aspx
    /// </code>
    /// <code>
    /// EmployeesCollection emps = new EmployeesCollection;
    /// 
    /// emps.Query.es.CountAll = true;
    /// emps.Query.Select
    /// (
    ///		emps.Query.LastName,
    ///		emps.Query.FirstName
    /// )
    /// .Where
    /// (
    ///		(emps.Query.LastName.Like("%A%") || emps.Query.LastName.Like("%O%")) &&
    ///		emps.Query.BirthDate.Between("1940-01-01", "2006-12-31")
    /// )
    /// .GroupBy
    /// (
    ///		emps.Query.LastName,
    ///		emps.Query.FirstName
    /// )
    /// .OrderBy
    /// (
    ///		emps.Query.LastName.Descending,
    ///		emps.Query.FirstName.Ascending
    /// );
    /// 
    /// emps.Query.Load();
    /// </code>
    /// </example>
    
    //[Serializable] 
    public class tgDynamicQuery : tgDynamicQuerySerializable
    {
        /// <summary>
        /// The Constructor
        /// </summary>
        public tgDynamicQuery()
        {

        }

        /// <summary>
        /// The Constructor used when using this query in a "Join"
        /// </summary>
        /// <param name="joinAlias">The alias of the associated Table to be used in the "Join"</param>
        public tgDynamicQuery(string joinAlias)
        {
            iData.JoinAlias = joinAlias;
        }

        /// <summary>
        /// Read-only metadata for the entity.
        /// </summary>
        /// <remarks>
        /// The sample below loops through the <see cref="tgColumnMetadataCollection"/> in provided
        /// by the <see cref="IMetadata"/> interface. There is a lot of useful information here, in fact,
        /// there is enough information for EntitySpaces to build all of the dynamic sql required during
        /// operations that use dynamic sql.
        /// <code>
        /// public partial class Employees : esEmployees
        /// {
        /// 	public void CustomMethod()
        /// 	{
        /// 		foreach(tgColumnMetadata col in this.Meta.Columns)
        /// 		{
        /// 			if(col.IsInPrimaryKey)
        /// 			{
        /// 				// do something ...
        /// 			}
        /// 		}
        /// 	}
        /// }
        /// </code>
        /// </remarks>
        /// <seealso cref="tgColumnMetadata"/>
        /// <seealso cref="tgColumnMetadataCollection"/>
        /// <seealso cref="tgProviderSpecificMetadata"/>
        virtual protected IMetadata Meta { get { return null; } }

        /// <summary>
        /// Called whenever the Entity needs a connection. This can be used to override the default connection 
        /// per object manually, or automatically by filling in the "Connection Name" on the "Generated Master"
        /// template. 
        /// </summary>
        /// <returns></returns>
        override protected string GetConnectionName()
        {
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        private void AssignProviderMetadata(tgDynamicQuerySerializable query, List<tgDynamicQuerySerializable> beenThere)
        {
            if (beenThere.Contains(query)) return;

            beenThere.Add(query);

            tgDynamicQuery theQuery = query as tgDynamicQuery;
            IDynamicQuerySerializableInternal iQuery = query as IDynamicQuerySerializableInternal;

            if (theQuery != null)
            {
                tgConnection conn = theQuery.tg2.Connection;

                if (iQuery.ProviderMetadata == null)
                {
                    tgProviderSpecificMetadata providerMetadata = theQuery.Meta.GetProviderMetadata(conn.ProviderMetadataKey);
                    iQuery.DataID = theQuery.Meta.DataID;
                    iQuery.Columns = theQuery.Meta.Columns;
                    iQuery.ProviderMetadata = providerMetadata;
                }

                iQuery.Catalog = conn.Catalog;
                iQuery.Schema = conn.Schema;
            }

            // This code is for proxies as they are unable to work with column and provider metadata
            // until serialized back to the server
            if (iQuery.SelectAll)
            {
                foreach (tgColumnMetadata col in (tgColumnMetadataCollection)iQuery.Columns)
                {
                    tgQueryItem item = new tgQueryItem(this, col.Name, col.esType);
                    query.Select(item);
                }
            }
            else 
            {
                List<tgQueryItem> columns = iQuery.SelectAllExcept;

                if (columns != null)
                {
                    foreach (tgColumnMetadata col in (tgColumnMetadataCollection)iQuery.Columns)
                    {
                        bool found = false;

                        for (int i = 0; i < columns.Count; i++)
                        {
                            if (col.Name == (string)columns[i])
                            {
                                found = true;
                                break;
                            }
                        }

                        if (found) continue;

                        tgExpression item = new tgQueryItem(this, col.Name, col.esType);
                        query.Select(item);
                    }
                }
            }

            foreach (tgDynamicQuerySerializable subQuery in iQuery.queries.Values)
            {
                AssignProviderMetadata(subQuery, beenThere);
            }

            if (iQuery.InternalSetOperations != null)
            {
                foreach (tgSetOperation setOperation in iQuery.InternalSetOperations)
                {
                    AssignProviderMetadata(setOperation.Query, beenThere);
                }
            }
        }

        /// <summary>
        /// This is called when DynamicQuery.Load() is called,
        /// this occurs when code like this is executed:  emps.Query.Load(), 
        /// by default this method does nothing.
        /// When overriding you don't use the internal keyword.
        /// </summary>
        /// <param name="Query">The DynamicQuery that was just loaded</param>        
        /// <param name="table">The DataTable as passed into Query.Load()</param>
        /// <returns>True if at least one record was loaded</returns>
        public delegate bool QueryLoadedDelegate(tgDynamicQuery Query, DataTable table);

        /// <summary>
        /// This is called when DynamicQuery.Load() is called,
        /// this occurs when code like this is executed:  emps.Query.Load(), 
        /// by default this method does nothing.
        /// When overriding you don't use the internal keyword.
        /// </summary>
        [OptionalFieldAttribute]
        public QueryLoadedDelegate OnLoadDelegate;

        /// <summary>
        /// This initializes the tgDataRequest for the query.
        /// </summary>
        /// <param name="request">The request to populate.</param>
        protected void PopulateRequest(tgDataRequest request)
        {
            IMetadata meta = this.Meta;

            tgConnection conn = this.tg2.Connection;
            tgProviderSpecificMetadata providerMetadata = meta.GetProviderMetadata(conn.ProviderMetadataKey);

            IDynamicQuerySerializableInternal iQuery = this as IDynamicQuerySerializableInternal;

            if ((this.queries != null && this.queries.Count > 0) || iQuery.InternalSetOperations != null)
            {
                AssignProviderMetadata(this, new List<tgDynamicQuerySerializable>());
            }

            string catalog = conn.Catalog;
            string schema = conn.Schema;

            iData.Catalog = catalog;
            iData.Schema = schema;
            iData.DataID = meta.DataID;
            iData.ProviderMetadata = providerMetadata;
            iData.Columns = meta.Columns;

            request.ConnectionString = conn.ConnectionString;
            request.CommandTimeout = conn.CommandTimeout;
            request.QueryType = tgQueryType.DynamicQuery;
            request.DynamicQuery = this;
            request.DataID = meta.DataID;
            request.ProviderMetadata = providerMetadata;

            request.Catalog = catalog;
            request.Schema = schema;
            request.Columns = meta.Columns;

            if (this.m_selectAll)
            {
                _selectAll();
            }

            if (tg.QuerySource == null || tg.QuerySource.Length == 0)
            {
                tg.QuerySource = providerMetadata.Source;
            }
        }

        #region Select Processing

        private void _selectAll()
        {
            if (this.m_selectAll)
            {
                foreach (tgColumnMetadata col in this.Meta.Columns)
                {
                    tgQueryItem item = new tgQueryItem(this, col.Name, col.esType);
                    this.Select(item);
                }

                this.m_selectAll = false;
            }
        }


        #endregion

        #region Load

        private void FixupSerializedQueries()
        {
            if (m_selectAll)
            {
                SelectAll();
            }
            else if (m_selectAllExcept != null)
            {
                SelectAllExcept(m_selectAllExcept.ToArray());
            }

            HookupWithNoLock(this);
        }

        /// <summary>
        /// Execute the Query and loads your BusinessEntity. 
        /// If you need to be notified that this is being called
        /// override BusinessEntity.Query.OnLoadEvent().
        /// </summary>
        /// <remarks>
        /// The default conjunction is AND.
        /// You can change the default conjunction this way:
        /// <code>
        /// emps.Query.es.DefaultConjunction = tgConjunction.Or;
        /// </code>
        /// </remarks>
        /// <returns>True if at least one record was loaded</returns>
        virtual public bool Load()
        {
            bool loaded = false;

            DataTable table = null;

            FixupSerializedQueries();

            tgDataRequest request = new tgDataRequest();
            this.PopulateRequest(request);

            tgDataProvider provider = new tgDataProvider();
            tgDataResponse response = provider.esLoadDataTable(request, this.tg2.Connection.ProviderSignature);

            table = response.Table;

            if (prefetchMaps != null)
            {
                foreach (tgPrefetchMap map in prefetchMaps)
                {
                    // Give our Prefetch Queries the proper connection strings
                    if (!map.Query.tg2.HasConnection)
                    {
                        string generatedName = this.GetConnectionName();

                        if (generatedName != null)
                        {
                            // Use the connection name typed into the generated master when they
                            // generated the code
                            map.Query.tg2.Connection.Name = generatedName;
                        }
                        else
                        {
                            // Use the connection from the Collection/Entity at the time they
                            // call Load()
                            map.Query.tg2.Connection.Name = this.connection.Name;
                        }
                    }

                    map.Table = map.Query.LoadDataTable();
                }
            }

            if (this.OnLoadDelegate != null)
            {
                loaded = OnLoadDelegate(this, table);
            }

            return loaded;
        }

        /// <summary>
        /// This merely parses and returns the SQL Syntax, no SQL is executed. 
        /// </summary>
        /// <remarks>
        /// The default conjunction is AND.
        /// You can change the default conjunction this way:
        /// <code>
        /// emps.Query.es.DefaultConjunction = tgConjunction.Or;
        /// </code>
        /// </remarks>
        /// <returns>The SQL Syntax, the same as query.es.LastQuery when a query is executed.</returns>
        virtual public string Parse()
        {
            FixupSerializedQueries();

            tgDataRequest request = new tgDataRequest();
            this.PopulateRequest(request);
            request.QueryType = tgQueryType.DynamicQueryParseOnly;

            tgDataProvider provider = new tgDataProvider();
            tgDataResponse response = provider.esLoadDataTable(request, this.tg2.Connection.ProviderSignature);

            return response.LastQuery;
        }

        /// <summary>
        /// Execute the Query and load a DataTable. 
        /// </summary>
        /// <returns>A DataTable containing the loaded records.</returns>
        virtual public DataTable LoadDataTable()
        {
            DataTable table = null;

            FixupSerializedQueries();

            tgDataRequest request = new tgDataRequest();
            this.PopulateRequest(request);

            tgDataProvider provider = new tgDataProvider();
            tgDataResponse response = provider.esLoadDataTable(request, this.tg2.Connection.ProviderSignature);

            table = response.Table;

            return table;
        }

        /// <summary>
        /// Execute the query and return a DataReader. You must use the 'using' syntax or Close the reader 
        /// when finished with it.
        /// </summary>
        /// <returns>The DataReader</returns>
        virtual public IDataReader ExecuteReader()
        {
            FixupSerializedQueries();

            tgDataRequest request = new tgDataRequest();
            this.PopulateRequest(request);

            tgDataProvider provider = new tgDataProvider();
            tgDataResponse response = provider.ExecuteReader(request, this.tg2.Connection.ProviderSignature);

            return response.DataReader;
        }

        /// <summary>
        /// Execute the query and return a single value. 
        /// </summary>
        /// <returns>The value</returns>
        virtual public object ExecuteScalar()
        {
            FixupSerializedQueries();

            tgDataRequest request = new tgDataRequest();
            this.PopulateRequest(request);

            tgDataProvider provider = new tgDataProvider();
            tgDataResponse response = provider.ExecuteScalar(request, this.tg2.Connection.ProviderSignature);

            return response.Scalar;
        }

        /// <summary>
        /// Execute the query and return a single value. 
        /// </summary>
        /// <returns>The value</returns>
        virtual public T ExecuteScalar<T>()
        {
            FixupSerializedQueries();

            tgDataRequest request = new tgDataRequest();
            this.PopulateRequest(request);

            tgDataProvider provider = new tgDataProvider();
            tgDataResponse response = provider.ExecuteScalar(request, this.tg2.Connection.ProviderSignature);

            return (T)response.Scalar;
        }

        #endregion

        #region Prefetch Support

        /// <summary>
        /// Used when Prefetching data. This is called once per each prefetched set of data 
        /// </summary>
        /// <remarks>
        /// The code below loads EmployeeID number 1, and prefetches it's Orders and OrderDetail records.
        /// We could also load many Employees via a Collection and prefetch their Orders and OrderDetail records.
        /// <code>
        /// // The Main Query
        /// EmployeesQuery q = new EmployeesQuery("e");
        /// q.Where(q.EmployeeID == 1);
        /// 
        /// // The OrdersCollection
        /// OrdersQuery o = q.Prefetch&lt;OrdersQuery&gt;(Employees.Prefetch_OrdersCollectionByEmployeeID);
        /// EmployeesQuery emp1 = o.GetQuery&lt;EmployeesQuery&gt;();
        /// o.Where(emp1.EmployeeID == 1);
        /// 
        /// // The OrdersDetailsCollection
        /// OrderDetailsQuery od = q.Prefetch&lt;OrderDetailsQuery&gt;(Employees.Prefetch_OrdersCollectionByEmployeeID, Orders.Prefetch_OrderDetailsCollectionByOrderID);
        /// EmployeesQuery emp2 = od.GetQuery&lt;EmployeesQuery&gt;();
        /// od.Where(emp2.EmployeeID == 1);
        /// 
        /// // Load It
        /// Employees emp = new Employees();
        /// emp.Load(q);
        /// </code>
        /// </remarks>        
        /// <typeparam name="T">The Type of the esDynamicQuery returned</typeparam> 
        /// <param name="maps">The Path to the data</param>
        /// <returns>The esDynamicQuery for the Type of query you intend to load</returns>
        public T Prefetch<T>(params tgPrefetchMap[] maps) where T : tgDynamicQuery
        {
            return this.Prefetch<T>(true, maps);
        }

        /// <summary>
        /// This Prefetch allows you to fill in the Select() statement for the query to control what columns are brought back
        /// </summary>
        /// <typeparam name="T">The Type of the esDynamicQuery returned</typeparam> 
        /// <param name="provideSelect">If true then you must fill in the Query.Select() clause</param>
        /// <param name="maps">The Path to the data</param>
        /// <returns>The esDynamicQuery for the Type of query you intend to load</returns>
        public T Prefetch<T>(bool provideSelect, params tgPrefetchMap[] maps) where T : tgDynamicQuery
        {
            if (maps != null)
            {
                if (prefetchMaps == null)
                {
                    prefetchMaps = new List<tgPrefetchMap>();
                }

                tgPrefetchParameters data = new tgPrefetchParameters();

                // Create the query, we do so in reverse order
                for (int i = maps.Length - 1; i >= 0; i--)
                {
                    tgPrefetchDelegate prefetchDelegate = maps[i].PrefetchDelegate;
                    prefetchDelegate(data);
                }

                // The path is the next to the last PropertyName
                string path = string.Empty;
                if (maps.Length > 1)
                {
                    path = maps[maps.Length - 2].PropertyName;
                }

                tgPrefetchMap rootMap = maps[maps.Length - 1];
                rootMap.Query = data.Root;
                rootMap.Path = path;
                prefetchMaps.Add(rootMap);

                if (provideSelect)
                {
                    rootMap.Query.Select(rootMap.Query);
                }

                return (T)data.Root;
            }

            return null;
        }

        /// <summary>
        /// This Prefetch call is a simpler API for when you are not interested in tweaking the Query
        /// </summary>
        /// <param name="maps">The Path to the data</param>
        public void Prefetch(params tgPrefetchMap[] maps)
        {
            if (maps != null)
            {
                if (prefetchMaps == null)
                {
                    prefetchMaps = new List<tgPrefetchMap>();
                }

                tgPrefetchParameters data = new tgPrefetchParameters();

                // Create the query, we do so in reverse order
                for (int i = maps.Length - 1; i >= 0; i--)
                {
                    tgPrefetchDelegate prefetchDelegate = maps[i].PrefetchDelegate;
                    prefetchDelegate(data);
                }

                // The path is the next to the last PropertyName
                string path = string.Empty;
                if (maps.Length > 1)
                {
                    path = maps[maps.Length - 2].PropertyName;
                }

                tgPrefetchMap rootMap = maps[maps.Length - 1];
                rootMap.Query = data.Root;
                rootMap.Path = path;
                prefetchMaps.Add(rootMap);

                rootMap.Query.Select(rootMap.Query);
            }
        }

        #endregion

        #region Overloads

        /// <summary>
        /// This method will create a Select statement for all of the columns in the entity except for the ones passed in.
        /// This is very useful when you want to eliminate blobs and other fields for performance.
        /// </summary>
        /// <param name="columns">The columns which you wish to exclude from the Select statement</param>
        /// <returns></returns>
        override public tgDynamicQuerySerializable SelectAllExcept(params tgQueryItem[] columns)
        {
            foreach (tgColumnMetadata col in this.Meta.Columns)
            {
                bool found = false;

                for (int i = 0; i < columns.Length; i++)
                {
                    if (col.Name == (string)columns[i])
                    {
                        found = true;
                        break;
                    }
                }

                if (found) continue;

                tgExpression item = new tgQueryItem(this, col.Name, col.esType);
                this.Select(item);
            }

            return this;
        }

        /// <summary>
        /// This method will select all of the columns that were present when you generated your
        /// classes as opposed to doing a SELECT *
        /// </summary>
        /// <returns></returns>
        override public tgDynamicQuerySerializable SelectAll()
        {
            foreach (tgColumnMetadata col in this.Meta.Columns)
            {
                tgQueryItem item = new tgQueryItem(this, col.Name, col.esType);
                this.Select(item);
            }

            return this;
        }

        #endregion

        #region Helper Routine
        private List<tgComparison> ProcessWhereItems(tgConjunction conj, params object[] theItems)
        {
            List<tgComparison> items = new List<tgComparison>();

            items.Add(new tgComparison(tgParenthesis.Open));

            bool first = true;

            tgComparison whereItem;
            int count = theItems.Length;

            for (int i = 0; i < count; i++)
            {
                object o = theItems[i];

                whereItem = o as tgComparison;
                if (whereItem != null)
                {
                    if (!first)
                    {
                        items.Add(new tgComparison(conj));
                    }
                    items.Add(whereItem);
                    first = false;
                }
                else
                {
                    List<tgComparison> listItem = o as List<tgComparison>;
                    if (listItem != null)
                    {
                        if (!first)
                        {
                            items.Add(new tgComparison(conj));
                        }
                        items.AddRange(listItem);
                        first = false;
                    }
                    else
                    {
                        throw new Exception("Unsupported Type");
                    }
                }
            }

            items.Add(new tgComparison(tgParenthesis.Close));

            return items;
        }
        #endregion Helper Routine

        #region es2

        /// <summary>
        /// This is to help hide some details from Intellisense.
        /// </summary>
        public DynamicQueryProps tg2
        {
            get
            {
                if (this.props == null)
                {
                    this.props = new DynamicQueryProps(this);
                }

                return this.props;
            }
        }

        [NonSerialized]
        private DynamicQueryProps props;

        /// <summary>
        /// The Dynamic Query properties.
        /// </summary>
        public new class DynamicQueryProps
        {
            /// <summary>
            /// The Dynamic Query properties.
            /// </summary>
            /// <param name="query">The esDynamicQuery's properties.</param>
            public DynamicQueryProps(tgDynamicQuery query)
            {
                this.dynamicQuery = query;
            }

            /// <summary>
            /// Returns true if this Query has been assigned it's own Connection
            /// </summary>
            public bool HasConnection
            {
                get
                {
                    return this.dynamicQuery.connection != null;
                }
            }

            /// <summary>
            /// Used only to override the IConnectionNameService API when in use
            /// </summary>
            /// <param name="connectionName">The Name of the connection</param>
            /// <returns>The newly created tgConnection Connection</returns>
            [EditorBrowsable(EditorBrowsableState.Never)]
            public tgConnection ConnectionServiceOverride(string connectionName)
            {
                if (this.dynamicQuery.connection == null)
                {
                    this.dynamicQuery.connection = new tgConnection();

                    this.dynamicQuery.connection.Name = connectionName;
                }

                return this.dynamicQuery.connection;
            }

            /// <summary>
            /// tgConnection Connection.
            /// </summary>
            public tgConnection Connection
            {
                get
                {
                    if (this.dynamicQuery.connection == null)
                    {
                        this.dynamicQuery.connection = new tgConnection();

                        if (tgConnection.ConnectionService != null)
                        {
                            this.dynamicQuery.connection.Name = tgConnection.ConnectionService.GetName();
                        }
                        else
                        {
                            string connName = this.dynamicQuery.GetConnectionName();
                            if (connName != null)
                            {
                                this.dynamicQuery.connection.Name = connName;
                            }
                        }
                    }

                    return this.dynamicQuery.connection;
                }
                set { this.dynamicQuery.connection = value; }
            }

            /// <summary>
            /// Used internally by Entityspaces, do not use this Property
            /// </summary>
            public List<tgPrefetchMap> PrefetchMaps
            {
                get
                {
                    return this.dynamicQuery.prefetchMaps;
                }
            }


            private tgDynamicQuery dynamicQuery;
        }
        #endregion

        private IDynamicQuerySerializableInternal iData
        {
            get
            {
                return this as IDynamicQuerySerializableInternal;
            }
        }

        /// <summary>
        /// Used when deserializing the queries send from the client side
        /// </summary>
        [NonSerialized]
        static Dictionary<string, IMetadata> metadataDictionary = new Dictionary<string, IMetadata>();

        /// <summary>
        /// This property is in "es2" because the client side queries cannot have connections
        /// </summary>
        [NonSerialized]
        private tgConnection connection;

        /// <summary>
        /// Holds the information for each Prefetch property to be loaded
        /// </summary>
        [NonSerialized]
        private List<tgPrefetchMap> prefetchMaps;
    }
}
