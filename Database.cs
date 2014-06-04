using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;

namespace UCIS {
	public class Database {
		private delegate object ConstructorDelegate();
		private ConstructorInfo _ConnectionConstructor;

		public Database(Type DBConnectionType, string connectionString) {
			this.ConnectionString = connectionString;
			_ConnectionConstructor = DBConnectionType.GetConstructor(new Type[] { });
		}

		public string ConnectionString { get; set; }

		public virtual IDbConnection GetConnection() {
			IDbConnection conn = (IDbConnection)_ConnectionConstructor.Invoke(null);
			conn.ConnectionString = ConnectionString;
			conn.Open();
			return conn;
		}

		private static IDbCommand PrepareQuery(IDbConnection Connection, string Query, params object[] Parameters) {
			IDbCommand Command = Connection.CreateCommand();
			Command.CommandType = CommandType.Text;
			Command.CommandText = Query;
			Command.Parameters.Clear();
			int ParameterI = 0;
			foreach (object Parameter in Parameters) {
				IDbDataParameter DBParameter = Command.CreateParameter();
				DBParameter.Direction = ParameterDirection.Input;
				DBParameter.ParameterName = "?" + ParameterI.ToString();
				DBParameter.Value = Parameter;
				Command.Parameters.Add(DBParameter);
				ParameterI++;
			}
			if (ParameterI > 0) Command.Prepare();
			return Command;
		}

		public IDbCommand PrepareQuery(string Query, params object[] Parameters) {
			IDbConnection Connection = GetConnection();
			try {
				return PrepareQuery(Connection, Query, Parameters);
			} catch (Exception) {
				Connection.Close();
				throw;
			}
		}

		public int NonQuery(string QueryString, params object[] Parameters) {
			using (IDbConnection connection = GetConnection()) {
				using (IDbCommand command = PrepareQuery(connection, QueryString, Parameters)) {
					return command.ExecuteNonQuery();
				}
			}
		}

		public object FetchField(string QueryString, params object[] Parameters) {
			using (IDbConnection connection = GetConnection()) {
				using (IDbCommand command = PrepareQuery(connection, QueryString, Parameters)) {
					return command.ExecuteScalar();
				}
			}
		}
		public object[] FetchRow(string QueryString, params object[] Parameters) {
			using (IDbConnection connection = GetConnection()) {
				using (IDbCommand command = PrepareQuery(connection, QueryString, Parameters)) {
					using (IDataReader Reader = command.ExecuteReader()) {
						if (!Reader.Read()) return null;
						object[] Result = new object[Reader.FieldCount];
						Reader.GetValues(Result);
						return Result;
					}
				}
			}
		}
		public object[][] FetchRows(string QueryString, params object[] Parameters) {
			using (IDbConnection connection = GetConnection()) {
				using (IDbCommand command = PrepareQuery(connection, QueryString, Parameters)) {
					using (IDataReader Reader = command.ExecuteReader()) {
						List<object[]> Result = new List<object[]>();
						while (Reader.Read()) {
							object[] ResultArray = new object[Reader.FieldCount];
							Reader.GetValues(ResultArray);
							Result.Add(ResultArray);
						}
						return Result.ToArray();
					}
				}
			}
		}
		public void ForEachRow(Action<Object[]> f, string QueryString, params object[] Parameters) {
			using (IDbConnection connection = GetConnection()) {
				using (IDbCommand command = PrepareQuery(connection, QueryString, Parameters)) {
					using (IDataReader Reader = command.ExecuteReader()) {
						while (Reader.Read()) {
							object[] ResultArray = new object[Reader.FieldCount];
							Reader.GetValues(ResultArray);
							f(ResultArray);
						}
					}
				}
			}
		}
		public void ForEachRow(Action<IDataRecord> callback, string query, params object[] parameters) {
			using (IDbConnection connection = GetConnection()) {
				using (IDbCommand command = PrepareQuery(connection, query, parameters)) {
					using (IDataReader Reader = command.ExecuteReader()) {
						while (Reader.Read()) callback(Reader);
					}
				}
			}
		}

		/*public DBReader GetReader(string QueryString, params object[] Parameters) {
			return new DBReader(PrepareQuery(QueryString, Parameters));
		}*/

		public IDataReader ExecuteReader(String QueryString, params Object[] Parameters) {
			IDbConnection connection = GetConnection();
			try {
				using (IDbCommand command = PrepareQuery(connection, QueryString, Parameters)) {
					return command.ExecuteReader(CommandBehavior.CloseConnection);
				}
			} catch {
				connection.Dispose();
				throw;
			}
		}

		public IEnumerable<IDataRecord> EnumerateRows(String query, params Object[] parameters) {
			IDbConnection connection = GetConnection();
			try {
				return new DataEnumerator(PrepareQuery(connection, query, parameters));
			} catch {
				connection.Dispose();
				throw;
			}
		}

		class DataEnumerator : IEnumerable<IDataRecord>, IEnumerator<IDataRecord> {
			IDbCommand command = null;
			IDataReader reader = null;
			public DataEnumerator(IDbCommand command) {
				this.command = command;
				try {
					this.reader = command.ExecuteReader();
				} catch {
					Dispose();
					throw;
				}
			}
			IEnumerator<IDataRecord> IEnumerable<IDataRecord>.GetEnumerator() {
				return this;
			}
			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
				return this;
			}
			public IDataRecord Current {
				get { return reader; }
			}
			object System.Collections.IEnumerator.Current {
				get { return reader; }
			}
			public bool MoveNext() {
				return reader.Read();
			}
			public void Reset() {
				throw new NotSupportedException();
			}
			public Object[] CurrentRow {
				get {
					object[] array = new object[reader.FieldCount];
					reader.GetValues(array);
					return array;
				}
			}
			public void Dispose() {
				if (reader != null) reader.Dispose();
				if (command != null) {
					IDbConnection connection = command.Connection;
					command.Dispose();
					connection.Dispose();
				}
			}
		}
	}
}